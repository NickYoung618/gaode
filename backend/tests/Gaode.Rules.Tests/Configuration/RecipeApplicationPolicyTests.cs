using Gaode.Application.Recipes;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Gaode.Rules.Tests.Configuration;

public sealed class RecipeApplicationPolicyTests
{
    [Theory]
    [InlineData("BA07/missing", null)]
    [InlineData("BA07/zero", 0)]
    [InlineData("BA07/negative", -1)]
    public void MissingOrInvalidBudgetNeverCreatesApplicationWindow(string id, int? value)
    {
        Assert.StartsWith("BA07/", id);
        Assert.Throws<InvalidOperationException>(() => RecipeApplicationCoordinator.RequireBudget(
            "budget", "2", "1.1", "Test", "Approved009Test", "digest", "snapshot", value));
    }
    [Fact]
    public void ProductionNeverFallsBackToTestValue() => Assert.Throws<InvalidOperationException>(() =>
        RecipeApplicationCoordinator.RequireBudget("budget", "2", "1.1", "Production", "NotApproved", "digest", "snapshot", 10000));
    [Theory]
    [InlineData("BA04/before", 9999, true)]
    [InlineData("BA04/exact", 10000, false)]
    [InlineData("BA04/after", 10001, false)]
    public void TotalWindowKeepsResponseBeforeDeadline(string id, int elapsed, bool allowed)
    {
        var clock = new FakeTimeProvider();
        var window = RecipeApplicationCoordinator.RegisterWindow(clock, "host-test", 10000, []);
        clock.Advance(TimeSpan.FromMilliseconds(elapsed));
        Assert.StartsWith("BA04/", id);
        Assert.Equal(allowed, window.Contains(clock.GetTimestamp()));
    }
    [Fact]
    public void ExistingDeadlineIsNotRefreshedByBinding()
    {
        var clock = new FakeTimeProvider();
        var frozenDeadline = clock.GetUtcNow().AddMilliseconds(2000);
        clock.Advance(TimeSpan.FromMilliseconds(500));
        var window = RecipeApplicationCoordinator.RegisterWindow(clock, "host-test", 10000, [frozenDeadline]);
        Assert.Equal(frozenDeadline, window.DeadlineUtc);
        clock.Advance(TimeSpan.FromMilliseconds(1500));
        Assert.False(window.Contains(clock.GetTimestamp()));
        Assert.Throws<TimeoutException>(() => RecipeApplicationCoordinator.RegisterWindow(clock, "host-test", 10000, [frozenDeadline]));
    }
}
