using System.Buffers.Binary;
using Gaode.Plc.Protocol;

namespace VirtualPlc;

public sealed record TestFeedbackFaultRecord(string Fault, Guid ConnectionId, ushort TransactionId,
    DateTimeOffset ReceivedAtUtc, DateTimeOffset AppliedAtUtc, string RequestPduHex,
    string ProcessedResponseHex, string Disposition)
{ public DateTimeOffset? DelayElapsedAtUtc { get; init; } }
internal enum TestResponseDisposition { Normal, Delay, Close }

// Two finite transport faults on the current independent X trigger, inside the independent
// VirtualPlc process. This has no Host state machine, business identity or store.
internal sealed class TestFeedbackFaults
{
    private readonly object gate = new();
    private readonly List<TestFeedbackFaultRecord> records = [];
    private SimulationFault? armed;
    private Guid? axisConnection;
    public (bool Accepted, string Message) Arm(SimulationFault fault)
    {
        lock (gate)
        {
            if (armed is not null || records.Count >= 8) return (false, "A finite Test transport fault is already armed or exhausted.");
            armed = fault; axisConnection = null;
            return (true, "Test transport fault armed for the next X-axis trigger.");
        }
    }
    public void MarkDelayElapsed(Guid connection, ushort transaction)
    {
        lock (gate)
        {
            var index = records.FindIndex(x => x.ConnectionId == connection && x.TransactionId == transaction);
            if (index < 0) throw new InvalidOperationException("ActualTestFaultRecordMissing");
            records[index] = records[index] with { DelayElapsedAtUtc = DateTimeOffset.UtcNow };
        }
    }
    public IReadOnlyList<TestFeedbackFaultRecord> Records() { lock (gate) return records.ToArray(); }
    public TestResponseDisposition AfterProcessedRequest(byte[] request, byte[] response, Guid connection,
        ushort transaction, DateTimeOffset received, byte[] responseFrame)
    {
        lock (gate)
        {
            if (armed is null || response.Length == 0 || response[0] != request[0]) return TestResponseDisposition.Normal;
            var axisTrigger = request.Length == 5 && request[0] == 5 &&
                BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(1, 2)) == PlcAddressMap.ToPduOffset(PlcAddressMap.Coils.XMoveStart) &&
                BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(3, 2)) == 0xFF00;
            if (axisTrigger) axisConnection = connection;
            var result = armed == SimulationFault.AxisWriteResponseLost && axisTrigger ? TestResponseDisposition.Close :
                armed == SimulationFault.AxisResponseDelayed && connection == axisConnection && request[0] == 3
                    ? TestResponseDisposition.Delay : TestResponseDisposition.Normal;
            if (result == TestResponseDisposition.Normal) return result;
            records.Add(new(armed.Value.ToString(), connection, transaction, received, DateTimeOffset.UtcNow,
                Convert.ToHexString(request), Convert.ToHexString(responseFrame), result.ToString()));
            armed = null;
            return result;
        }
    }
}
