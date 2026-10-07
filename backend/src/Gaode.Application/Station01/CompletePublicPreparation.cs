using Gaode.Application.Motion;
using Gaode.Application.Ports;
using Gaode.Application.Station01.Steps;
using Gaode.Domain.Station01;

namespace Gaode.Application.Station01;

public sealed class CompletePublicPreparation(MotionCoordinator motion)
{
    public async Task<PublicPreparationHandoffV2> ExecuteAsync(RunExecution run, ControlLatch control,
        StartPreparationEvidence start, ThreeDEvidence threeD, FEvidence f,
        CancellationToken cancellationToken)
    {
        var observed = motion.Observe();
        var evidence = new CompletionEvidence(start.Accepted, start.DeviceReady, true, true, true, true, true, true,
            threeD.Media.StorageState == "FileCompleted" && f.Media.StorageState == "FileCompleted",
            observed.HasReliableObservation && observed.SafetyAssessment == SafetyAssessment.Clear, !motion.Unknown,
            run.PersistedRevision > 0);
        if (control.AdmissionClosed || !CompletionPolicy.CanComplete(evidence))
            throw new InvalidOperationException("公共阶段完成依据不足");
        var handoff = StageHandoffBuilder.BuildV2(run, start, threeD, f, observed.MotionAvailability.ToString());
        await run.ReportAsync(handoff: HandoffState.Saving);
        handoff = await run.SaveHandoffV2Async(handoff, cancellationToken);
        // 物理占用仍由MotionCoordinator持有；这里不释放、松夹或启动下一阶段。
        return handoff;
    }
}
