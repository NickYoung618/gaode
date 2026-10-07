using Gaode.Application.Motion;
using Gaode.Domain.Station01;
using Gaode.Diagnostics;

namespace Gaode.Application.Station01;

/// <summary>Consumes ordinary pause only after the current logical action is saved and cleared.</summary>
public sealed class NormalPauseBoundary(Station01Coordinator coordinator,
    MotionCoordinator motion, TimeProvider clock)
{
    public async Task WaitAsync(Guid runId, RunState resumeState, string boundary,
        long epoch, DateTimeOffset? deadline,
        Func<string, RunState, CancellationToken, Task> save,
        CancellationToken ct)
    {
        var control = coordinator.Control(runId) ?? throw new InvalidOperationException("RunNotFound");
        if (!control.PauseRequested)
        {
            if (coordinator.AdmissionClosed || control.CancelRequested || control.SafetyFault)
                throw new InvalidOperationException("PauseBoundaryControlClosed");
            return;
        }
        if (motion.Unknown || motion.CurrentAction is not null)
            throw new InvalidOperationException("PauseBoundaryActionNotCleared");
        RequireSafe(control, epoch, deadline);
        await save("NormalPaused", RunState.Paused, ct);
        await coordinator.SetAsync(runId, s => s.Next(RunState.Paused) with
        { Events = [..s.Events, "NormalPaused:" + boundary] }, control: true).WaitAsync(ct);
        RuntimeDiagnostics.Record("NormalPause", "Paused", runId,
            new { boundary, epoch, deadline, resumeState = resumeState.ToString() });
        while (control.PauseRequested)
        {
            ct.ThrowIfCancellationRequested();
            RequireSafe(control, epoch, deadline);
            await Task.Delay(TimeSpan.FromMilliseconds(50), clock, ct);
        }
        RequireSafe(control, epoch, deadline);
        await save("NormalContinued", resumeState, ct);
        await coordinator.SetAsync(runId, s => s.Next(resumeState) with
        { Events = [..s.Events, "NormalContinued:" + boundary] }, control: true).WaitAsync(ct);
        RuntimeDiagnostics.Record("NormalPause", "Continued", runId,
            new { boundary, epoch, deadline, sameRun = true, noRepeatedPublicPreparation = true });
    }

    private void RequireSafe(ControlLatch control, long epoch, DateTimeOffset? deadline)
    {
        var observed = motion.Observe();
        if (coordinator.AdmissionClosed || control.CancelRequested || control.SafetyFault ||
            !observed.HasReliableObservation || observed.OperatingMode != OperatingMode.Automatic || observed.SafetyAssessment != SafetyAssessment.Clear ||
            observed.ConnectionEpoch != epoch)
            throw new InvalidOperationException("PauseBoundarySafetyOrEpochChanged");
        if (deadline is { } end && clock.GetUtcNow() >= end)
            throw new TimeoutException("PauseBoundaryStageDeadlineExceeded");
    }
}
