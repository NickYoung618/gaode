namespace Gaode.Domain.Station01;

public sealed record HeightSample(string SourceElementId, double? RawValue, string? Unit,
    string? Datum, HeightValidity Validity, string? Reason)
{
    public double? NormalizedValue => Validity == HeightValidity.Valid ? RawValue : null;

    public static HeightSample FromRaw(string id, double? value, string? unit, string? datum)
    {
        var validity = value is null || !double.IsFinite(value.Value) ? HeightValidity.NonFinite :
            string.IsNullOrWhiteSpace(unit) ? HeightValidity.MissingUnit :
            string.IsNullOrWhiteSpace(datum) ? HeightValidity.MissingDatum : HeightValidity.Valid;
        return new(id, value, unit, datum, validity, validity == HeightValidity.Valid ? null : validity.ToString());
    }
}

public sealed record HeightResult(Guid RunId, Guid CaptureId, Guid CallId,
    string ScopeId, string ScopeVersion, IReadOnlyList<HeightSample> Samples, AlgorithmState TechnicalState);
