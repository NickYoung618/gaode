using Gaode.Application.Ports;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Diagnostics;

namespace Gaode.Application.Configuration;

public sealed record PublicPositionCandidate(FixedPoint Point, PositionObservation Actual, string Source);

public sealed class PublicPositionTeaching(IPublicConfiguration configurations, IPlcStatePort device)
{
    public LoadedConfiguration<PublicConfiguration> Read(ConfigReference reference) => configurations.LoadPublic(reference);

    public PublicPositionCandidate ReadCurrent(ConfigReference reference, string kind)
    {
        var configuration = Read(reference).Value;
        var target = kind switch {
            "ThreeD" => configuration.Motion.Points.ThreeD,
            "ManualLoading" => configuration.Motion.Points.Unload ??
                throw new ConfigurationException("ManualLoadingPositionMissing", "公共上下料位置未配置"),
            _ => throw new ArgumentException("PublicPositionKindInvalid") };
        var observed = device.Observe();
        var actual = observed.PositionForPurpose(kind == "ThreeD" ? "DetectionZ" : "GrabZ");
        var allowedOrigin = actual is not null && (actual.Origin is { Provider: DeviceProvider.Real, Quality: EvidenceQuality.Measured } ||
            configuration.Purpose == "Test" && actual.Origin is { Provider: DeviceProvider.Virtual or DeviceProvider.Simulated,
                Quality: EvidenceQuality.Derived or EvidenceQuality.Measured });
        if (!observed.HasReliableObservation || actual is not { IsReliable: true } || !allowedOrigin ||
            actual.Identity.ConnectionEpoch != observed.ConnectionEpoch || actual.CoordinateFrame != target.Frame ||
            string.IsNullOrWhiteSpace(actual.UnitBasis) || !actual.UnitBasis.EndsWith(":" + target.Unit, StringComparison.Ordinal))
            throw new ConfigurationException("PositionTeachingUnavailable", "当前没有符合轴、坐标系和单位合同的可靠实测位置");
        var candidate = target with { X = actual.ActualX!.Value, Y = actual.ActualY!.Value, Z = actual.ActualZ!.Value };
        RuntimeDiagnostics.Record("PublicPositions", "CurrentPositionRead", null,
            new { kind, reference, actual.Identity, actual.Origin, disposition = "CandidateRequiresOperatorConfirmation" });
        return new(candidate, actual, configuration.Purpose + "/" + actual.Origin.Provider);
    }

    public LoadedConfiguration<PublicConfiguration> Save(ConfigReference reference, FixedPoint threeD,
        FixedPoint manualLoading, string expectedDigest, string operatorId)
    {
        var saved = configurations.SavePublicPositions(reference, threeD, manualLoading, expectedDigest);
        RuntimeDiagnostics.Record("PublicPositions", "Saved", null,
            new { reference, saved.Digest, operatorId, saved.SourceFile, points = saved.Value.Motion.Points });
        return saved;
    }
}
