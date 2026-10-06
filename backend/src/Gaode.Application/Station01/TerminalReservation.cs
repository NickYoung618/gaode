namespace Gaode.Application.Station01;

public sealed class TerminalReservation(int capacity)
{
    private int _used;
    public int Used => Volatile.Read(ref _used);
    public bool TryReserve()
    {
        while (true)
        {
            var current = Volatile.Read(ref _used);
            if (current >= capacity) return false;
            if (Interlocked.CompareExchange(ref _used, current + 1, current) == current) return true;
        }
    }
    public void Release() => Interlocked.Decrement(ref _used);
}
