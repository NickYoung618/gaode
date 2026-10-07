namespace Gaode.Domain.Station01;

public enum FRecognitionState { NoResponse, NoCode, Unique, Conflict }
public enum FParseState { NotAttempted, NotDefined, Parsed, InvalidFormat }

public sealed record FCodeResult(bool ResponseReceived, IReadOnlyList<string>? RawCandidates,
    IReadOnlyList<string> DistinctRawValues, FRecognitionState RecognitionState,
    string? PrimaryCode, FParseState ParseState, string? TrayIdentifier);
