namespace Gaode.Domain.Station01;

public static class FCodePolicy
{
    public static bool CanBindRecipe(AlgorithmState algorithmState, FCodeResult result) =>
        algorithmState == AlgorithmState.Success && result.ResponseReceived &&
        result.RecognitionState == FRecognitionState.Unique &&
        !string.IsNullOrWhiteSpace(result.PrimaryCode) &&
        result.ParseState is FParseState.Parsed or FParseState.NotDefined;

    public static FCodeResult Evaluate(bool responseReceived, IReadOnlyList<string>? raw,
        Func<string, (bool Valid, string? Value)>? parser = null)
    {
        if (!responseReceived)
            return new(false, null, [], FRecognitionState.NoResponse, null, FParseState.NotAttempted, null);
        var source = raw ?? [];
        var distinct = source.Distinct(StringComparer.Ordinal).ToArray();
        if (distinct.Length == 0)
            return new(true, source, distinct, FRecognitionState.NoCode, null, FParseState.NotAttempted, null);
        if (distinct.Length > 1)
            return new(true, source, distinct, FRecognitionState.Conflict, null, FParseState.NotAttempted, null);
        if (parser is null)
            return new(true, source, distinct, FRecognitionState.Unique, distinct[0], FParseState.NotDefined, null);
        var result = parser(distinct[0]);
        return new(true, source, distinct, FRecognitionState.Unique, distinct[0],
            result.Valid ? FParseState.Parsed : FParseState.InvalidFormat, result.Value);
    }
}
