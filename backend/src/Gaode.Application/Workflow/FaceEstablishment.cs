using Gaode.Application.Ports;
using Gaode.Domain.Station01;

namespace Gaode.Application.Workflow;

public static class FaceEstablishment
{
    public static bool Confirms(FlipRequest request, DeviceActionEvidence evidence) =>
        evidence.IsCorrelated && evidence.Correlation == request.Correlation &&
        evidence.Meaning == DeviceCompletionMeaning.FlipCompleted && request.TransitionId != Guid.Empty &&
        evidence.TransitionId == request.TransitionId;

    public static bool Confirms(PutBackRequest request, DeviceActionEvidence evidence) =>
        evidence.IsCorrelated && evidence.Correlation == request.Correlation &&
        evidence.Meaning == DeviceCompletionMeaning.PutBackCompleted && request.TransitionId != Guid.Empty &&
        evidence.TransitionId == request.TransitionId;
}
