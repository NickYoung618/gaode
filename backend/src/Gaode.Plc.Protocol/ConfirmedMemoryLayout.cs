using System.Text.Json;

namespace Gaode.Plc.Protocol;

public sealed record ConfirmedMemoryPoint(string Id, string SourceName, string Direction,
    int MemoryByteAddress, string ValueType, string SourceType, string InitialValue,
    string Comment, string SourceFile, int SourceRow);
public sealed record ConfirmedMemorySource(string File, string Sha256);

/// <summary>User-confirmed memory addresses; network mapping is a separate field decision.</summary>
public sealed class ConfirmedMemoryLayout
{
    public const string CurrentId = "confirmed-20261006-v2";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public int SchemaVersion { get; init; }
    public string LayoutId { get; init; } = "";
    public string SourceReference { get; init; } = "";
    public ConfirmedMemorySource[] Sources { get; init; } = [];
    public ConfirmedMemoryPoint[] Points { get; init; } = [];

    public static ConfirmedMemoryLayout Load()
    {
        using var stream = typeof(ConfirmedMemoryLayout).Assembly.GetManifestResourceStream("Gaode.Plc.Protocol.confirmed-points.json")
            ?? throw new InvalidOperationException("ConfirmedMemoryLayoutMissing");
        return JsonSerializer.Deserialize<ConfirmedMemoryLayout>(stream, Json)!;
    }

    public ProtocolDefinition CreateDefinition(FieldAddressProfile profile)
    {
        profile.Validate(LayoutId);
        var fields = new List<PlcPoint>();
        foreach (var point in Points)
        {
            var required = SemanticField(point);
            if (required is null) continue;
            var type = Enum.Parse<PlcValueType>(point.ValueType);
            fields.Add(required with
            {
                DocumentNumber = profile.PduOffset(point) + 1,
                DocumentAddress = "%MB" + point.MemoryByteAddress,
                Area = PlcArea.HoldingRegister,
                ValueType = type,
                RegisterCount = type == PlcValueType.Float32 ? 2 : 1,
                ByteOffset = type == PlcValueType.BoolByte ? point.MemoryByteAddress % 2 : 0
            });
        }
        return new(fields, SiteAlarmBits, 1, 65536, Enum.Parse<Float32ByteOrder>(profile.FloatOrder))
        {
            Purpose = "Production", LayoutId = LayoutId, ByteOrderForBools = Enum.Parse<BoolByteOrder>(profile.BoolByteOrder),
            SourceReference = $"{LayoutId};{string.Join(';', Sources.Select(s => s.File + ':' + s.Sha256))};field:{profile.Source}"
        };
    }
    public IReadOnlyDictionary<SignalId, PlcPoint> RequiredSemanticFields => Points.Select(SemanticField)
        .OfType<PlcPoint>().ToDictionary(p => p.Id);
    private static PlcPoint? SemanticField(ConfirmedMemoryPoint point)
    {
        var required = point.Id switch
        {
            "PC_Start_Cmd" => new PlcPoint(SignalId.PcStartCmd, 1, "%MB2007", "PC_Start_Cmd",
                PlcDirection.PcToPlc, PlcValueType.BoolByte) { Writer = PlcWriter.Pc, ClearWriter = PlcWriter.Pc },
            "Alarm_Code" => ConfirmedProtocol.RequiredFields[SignalId.AlarmBits],
            "Alarm_Level" => ConfirmedProtocol.RequiredFields[SignalId.AlarmSeverity],
            "Model_Number" => ConfirmedProtocol.RequiredFields[SignalId.ModelPayload] with { Id = SignalId.ModelNumber },
            _ => ConfirmedProtocol.RequiredFields.Values.SingleOrDefault(f => f.Name == point.Id)
        };
        if (required is null && IndependentSafetySignals.TryGetValue(point.Id, out var safetyId))
            required = new(safetyId, 1, "%MB" + point.MemoryByteAddress, point.Id,
                PlcDirection.PlcToPc, PlcValueType.BoolByte)
                { Writer = PlcWriter.Plc, ClearWriter = PlcWriter.Plc };
        if (required is null) return null;
        var type = Enum.Parse<PlcValueType>(point.ValueType);
        return required with { Name = point.Id, ValueType = type, Area = PlcArea.HoldingRegister,
            RegisterCount = type == PlcValueType.Float32 ? 2 : 1,
            ByteOffset = type == PlcValueType.BoolByte ? point.MemoryByteAddress % 2 : 0 };
    }
    public static readonly IReadOnlyDictionary<string, SignalId> IndependentSafetySignals = new Dictionary<string, SignalId>
    {
        ["EStop_Active"] = SignalId.EStopActive, ["X_Axis_Alarm"] = SignalId.XAxisAlarm,
        ["Y_Axis_Alarm"] = SignalId.YAxisAlarm, ["Z_Camera_Axis_Alarm"] = SignalId.ZCameraAxisAlarm,
        ["Z_Scan_Axis_Alarm"] = SignalId.ZScanAxisAlarm, ["Z_Flip_Axis_Alarm"] = SignalId.ZFlipAxisAlarm,
        ["Rotate_Axis_Alarm"] = SignalId.RotateAxisAlarm, ["Flip_Axis_Alarm"] = SignalId.FlipAxisAlarm,
        ["PC_Communication_Alarm"] = SignalId.PcCommunicationAlarm, ["PC_Alarm"] = SignalId.PcAlarm
    };
    private static readonly AlarmBitDefinition[] SiteAlarmBits =
    [new("LightCurtain", SignalId.AlarmBits, 0, 3), new("EmergencyStop", SignalId.AlarmBits, 1, 3),
     new("SafetyDoor", SignalId.AlarmBits, 2, 3), new("ServoOverload", SignalId.AlarmBits, 3, 3),
     new("Communication", SignalId.AlarmBits, 4, 3), new("PlcInternal", SignalId.AlarmBits, 5, 3),
     new("Heartbeat", SignalId.AlarmBits, 6, 0), new("PcAlarm", SignalId.AlarmBits, 7, 3)];
}

public sealed class FieldAddressProfile
{
    public string LayoutId { get; init; } = "";
    public bool Confirmed { get; init; }
    public string Source { get; init; } = "";
    public int? PcPduBase { get; init; }
    public int? PlcPduBase { get; init; }
    public string PlcArea { get; init; } = "";
    public string BoolByteOrder { get; init; } = "";
    public string FloatOrder { get; init; } = "";

    public static ProtocolDefinition LoadDefinition(string path)
    {
        var profile = JsonSerializer.Deserialize<FieldAddressProfile>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException("FieldAddressProfileMissing");
        return ConfirmedMemoryLayout.Load().CreateDefinition(profile);
    }

    public void Validate(string expectedLayout)
    {
        if (LayoutId != expectedLayout || !Confirmed || string.IsNullOrWhiteSpace(Source))
            throw new InvalidOperationException("FieldAddressMappingUnconfirmed:" + expectedLayout);
        if (PcPduBase is not (>= 0 and <= 65508) || PlcPduBase is not (>= 0 and <= 65488))
            throw new InvalidOperationException("FieldAddressMappingOutOfRange");
        if (PlcArea != "HoldingRegister") throw new InvalidOperationException("FormalFeedbackAreaUnsupported:" + PlcArea);
        if (!Enum.TryParse<Gaode.Plc.Protocol.BoolByteOrder>(BoolByteOrder, out var boolOrder) || !Enum.IsDefined(boolOrder) ||
            !Enum.TryParse<Float32ByteOrder>(FloatOrder, out var floatOrder) || !Enum.IsDefined(floatOrder))
            throw new InvalidOperationException("FieldByteOrderUnconfirmed");
    }

    public int PduOffset(ConfirmedMemoryPoint point) => point.Direction switch
    {
        "PC->PLC" => PcPduBase!.Value + (point.MemoryByteAddress - 2000) / 2,
        "PLC->PC" => PlcPduBase!.Value + (point.MemoryByteAddress - 6000) / 2,
        _ => throw new InvalidDataException("UnknownSignalDirection")
    };
}
