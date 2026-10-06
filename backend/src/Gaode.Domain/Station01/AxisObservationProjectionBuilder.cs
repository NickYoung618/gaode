namespace Gaode.Domain.Station01;

// A display of observed values, never a position authorization or target echo.
public static class AxisObservationProjectionBuilder
{
    public static IReadOnlyList<AxisObservationProjection> From(DeviceObservation observation, string? evidenceRef)
    {
        if (observation.Position?.Identity is not { IsValid: true } identity || observation.AxisPositions is not { } axes) return [];
        return new[] { ("X", axes.X), ("Y", axes.Y), ("CameraZ", axes.DetectionZ), ("ScanZ", axes.ScanZ), ("GrabZ", axes.GrabZ) }
            .Select(a => new AxisObservationProjection(a.Item1, a.Item2 is { } value && double.IsFinite(value) ? value : null,
                null, identity.Reliability.ToString(), identity.SampleEndedUtc, identity.ConnectionEpoch, evidenceRef)).ToArray();
    }

    public static IReadOnlyList<AxisObservationProjection> From(PositionReachedEvidence evidence, string evidenceRef)
    {
        var actual = evidence.Actual;
        if (!evidence.Matched || !actual.Identity.IsValid) return [];
        var values = new List<(string Axis, double? Value)> { ("X", actual.ActualX), ("Y", actual.ActualY) };
        var z = actual.AxisPurpose switch { "DetectionZ" => "CameraZ", "ScanZ" => "ScanZ", "GrabZ" => "GrabZ", _ => null };
        if (z is not null) values.Add((z, actual.ActualZ));
        return values.Select(a => new AxisObservationProjection(a.Axis, a.Value, evidence.Target.Unit,
            actual.Identity.Reliability.ToString(), actual.Identity.SampleEndedUtc, actual.Identity.ConnectionEpoch, evidenceRef)).ToArray();
    }
}
