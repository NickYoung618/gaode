using Gaode.Application.Ports;

namespace Gaode.Application.Station01;

public enum EvidenceDisposition { Accepted, Duplicate, Late, Rejected }

public static class LateEvidencePolicy
{
    public static EvidenceDisposition Classify(bool envelopeMatches, long eventEpoch,
        long expectedEpoch, bool operationClosed, bool alreadySeen)
    {
        if (!envelopeMatches || eventEpoch != expectedEpoch) return EvidenceDisposition.Rejected;
        if (alreadySeen) return EvidenceDisposition.Duplicate;
        return operationClosed ? EvidenceDisposition.Late : EvidenceDisposition.Accepted;
    }
}
