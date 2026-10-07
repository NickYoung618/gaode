using Gaode.Domain.Station01;

namespace Gaode.Application.Timing;

public static class ActionWindows
{
    // Translate an already frozen UTC interval into this process's monotonic domain, without refreshing it.
    public static ActionWindow FromUtc(TimeProvider clock, DateTimeOffset started, DateTimeOffset due, string clockId)
    {
        var utc = clock.GetUtcNow(); var tick = clock.GetTimestamp();
        return new(checked(tick + (long)((started - utc).TotalSeconds * clock.TimestampFrequency)),
            checked(tick + (long)((due - utc).TotalSeconds * clock.TimestampFrequency)), clockId, started, due);
    }
    public static ActionWindow From(TimeProvider clock, long startTick, long dueTick, string clockId)
    {
        var now = clock.GetUtcNow(); var tick = clock.GetTimestamp();
        return new(startTick, dueTick, clockId,
            now.AddSeconds((startTick - tick) / (double)clock.TimestampFrequency),
            now.AddSeconds((dueTick - tick) / (double)clock.TimestampFrequency));
    }
    public static ActionWindow Start(TimeProvider clock, int budgetMs, string clockId)
    {
        if (budgetMs <= 0) throw new ArgumentOutOfRangeException(nameof(budgetMs));
        var start = clock.GetTimestamp();
        return From(clock, start, checked(start + (long)(clock.TimestampFrequency * (budgetMs / 1000d))), clockId);
    }
}
