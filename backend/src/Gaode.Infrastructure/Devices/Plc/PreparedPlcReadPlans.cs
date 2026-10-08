using System.Collections.Immutable;
using Gaode.Plc.Protocol;

namespace Gaode.Infrastructure.Devices.Plc;

internal sealed record PreparedPlcReadPlan(Guid AdmissionId, UInt128 FieldMask, ImmutableArray<SignalReadGroup> Blocks);

// Finite subsets admitted with the immutable mapping; no dynamic hot-path planning.
internal sealed class PreparedPlcReadPlans
{
    internal static readonly SignalId[] Axes = [SignalId.XPosConfirmed, SignalId.YPosConfirmed,
        SignalId.ZCameraPosConfirmed, SignalId.ZScanPosConfirmed, SignalId.ZGrapPosConfirmed];
    internal static readonly SignalId[] Starts = [SignalId.XMoveStart, SignalId.YMoveStart,
        SignalId.ZCameraMoveStart, SignalId.ZScanMoveStart, SignalId.ZGrabMoveStart];
    internal static readonly SignalId[] Position = [SignalId.MachineCurrentPosX, SignalId.MachineCurrentPosY,
        SignalId.MachineCurrentPosZ, SignalId.ScanCurrentPosZ, SignalId.FlipGrapCurrentPosZ];
    internal static readonly SignalId[] Base = [SignalId.PlcSystemFault, SignalId.PlcModeAuto,
        SignalId.PlcReadyState, SignalId.ManualZoneOccupied, SignalId.GrabActiveId, .. Starts,
        SignalId.AlarmBits, SignalId.AlarmSeverity, .. Axes];
    internal static readonly SignalId[] BaseWithoutAxes = Base.Except(Axes).ToArray();
    internal static readonly SignalId[] BaseWithoutGripper = Base.Where(id=>id!=SignalId.GrabActiveId).ToArray();
    internal static readonly SignalId[] BaseWithoutAxesOrGripper = BaseWithoutAxes.Where(id=>id!=SignalId.GrabActiveId).ToArray();
    internal static readonly SignalId[] Transfer = [SignalId.SortingCmd, SignalId.SortingExecStatus, .. Position];
    internal static readonly SignalId[] Gripper = [SignalId.GrabActiveId];
    internal static readonly SignalId[] FlipClear = [SignalId.FlipSorting, SignalId.FlipStatus, SignalId.FlipUnloadStatus];
    internal static readonly SignalId[] Rotation = [SignalId.RotateStart, SignalId.RPosConfirmed, SignalId.MachineCurrentPosR];
    internal static readonly SignalId[] ResetPreconditions = [SignalId.PcSystemReady, SignalId.SoftStopCmd, SignalId.SystemResetCmd];
    private readonly Dictionary<UInt128, PreparedPlcReadPlan> plans = [];
    internal Guid AdmissionId { get; } = Guid.NewGuid();
    internal int Count => plans.Count;
    internal PreparedPlcReadPlans(ProtocolDefinition definition)
    {
        foreach (var field in definition.Fields) Add([field.Id]);
        Add(definition.Fields.Select(f => f.Id)); Add(Base); Add(BaseWithoutAxes); Add(BaseWithoutGripper); Add(BaseWithoutAxesOrGripper); Add(Position); Add(Transfer); Add(Gripper); Add(Rotation); Add(FlipClear);
        Add([.. Base, .. Position]);
        if (definition.IsSiteLayout) Add(ResetPreconditions);
        Add([SignalId.PlcReadyState, SignalId.PlcModeAuto, SignalId.PlcSystemFault]);
        // Current axis batches are finite. Include every legal subset once at admission.
        for (var mask = 1; mask < 32; mask++)
            foreach (var family in new[] { Starts, Axes, Position })
                Add(family.Where((_, index) => (mask & (1 << index)) != 0));
        PlcCommunicationMeasurement.Count("AdmissionPreparations");
        PlcCommunicationMeasurement.Count("PreparedPlanCount", plans.Count);
        void Add(IEnumerable<SignalId> fields)
        {
            var values = fields.ToArray(); var key = Key(values);
            if (plans.ContainsKey(key)) return;
            if (definition.IsSiteLayout)
            {
                values = values.Where(id => definition.Fields.Any(f => f.Id == id)).ToArray();
                if (values.Contains(SignalId.AlarmBits)) values = values.Concat(ConfirmedMemoryLayout.IndependentSafetySignals.Values).Distinct().ToArray();
            }
            var blocks = definition.ReadPlan(values);
            if (key == Key(Transfer)) blocks = blocks.OrderBy(b => b.Fields.Contains(SignalId.SortingExecStatus) ? 0 : 1).ToImmutableArray();
            var violations = definition.ValidatePlan(blocks);
            if (violations.Count != 0) throw new ProtocolDefinitionException(violations);
            plans.Add(key, new(AdmissionId, key, blocks));
        }
    }
    internal PreparedPlcReadPlan Get(IEnumerable<SignalId> fields) => plans.TryGetValue(Key(fields), out var plan)
        ? plan : throw new InvalidOperationException("ReadPurposeNotPrepared");
    internal void RequireOwned(PreparedPlcReadPlan plan)
    {
        if (plan.AdmissionId != AdmissionId || !plans.TryGetValue(plan.FieldMask, out var expected) || !ReferenceEquals(plan, expected))
            throw new InvalidOperationException("PreparedPlanMappingMismatch");
    }
    private static UInt128 Key(IEnumerable<SignalId> fields)
    {
        UInt128 key = 0;
        foreach (var id in fields)
        {
            if ((int)id is < 0 or > 127 || !Enum.IsDefined(id)) throw new InvalidOperationException("UnconfirmedSignal");
            key |= (UInt128)1 << (int)id;
        }
        return key;
    }
}

internal sealed class PlcExchangeClock
{
    internal static readonly AsyncLocal<PlcExchangeClock?> Current = new();
    internal long Queued, Granted, Sent, Ended, ServiceEnded;
    internal PlcDispatchTiming? Dispatch;
    internal DateTimeOffset StartedUtc, EndedUtc;
}
internal sealed record PlcReadStamp(long Queued, long Sent, long Ended, DateTimeOffset StartedUtc, DateTimeOffset EndedUtc);
