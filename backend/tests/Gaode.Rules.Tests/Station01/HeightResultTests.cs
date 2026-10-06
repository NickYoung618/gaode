using Gaode.Domain.Station01;
using Xunit;

namespace Gaode.Rules.Tests.Station01;

public sealed class HeightResultTests
{
    [Fact]
    public void MissingOrInvalidHeightNeverBecomesZeroOrOldValue()
    {
        var missing = HeightSample.FromRaw("partless-source", null, "mm", "SIM_REFERENCE");
        var noDatum = HeightSample.FromRaw("source-b", 12.0, "mm", null);
        Assert.Equal(HeightValidity.NonFinite, missing.Validity);
        Assert.Equal(HeightValidity.MissingDatum, noDatum.Validity);
        Assert.Null(missing.NormalizedValue);
        Assert.Null(noDatum.NormalizedValue);
        Assert.Equal(12.0, noDatum.RawValue);
    }
}
