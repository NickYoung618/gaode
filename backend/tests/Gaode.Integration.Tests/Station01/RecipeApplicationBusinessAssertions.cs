using Gaode.Integration.Tests.Support;
using System.Diagnostics;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Application.Station01;
using Gaode.Domain.Station01;
using Xunit;

namespace Gaode.Integration.Tests.Station01;

// Frozen business assertions shared with the actual TCP observation component.
// No addresses, raw values, internal phases or communication implementation types.
internal static class RecipeApplicationBusinessAssertions
{
    internal static void AssertClosed(RunApiSnapshot stopped,ActionWindow window,long intentTick,
        JsonElement[] events,DeviceObservation atFailure,DeviceObservation current)
    {
        Assert.NotEqual("Bound", stopped.RecipeState);
        Assert.NotEqual(HandoffState.Ready, stopped.Handoff);
        Assert.DoesNotContain(stopped.Events, e => e.StartsWith("DetectionRequestPrepared:", StringComparison.Ordinal));
        Assert.Equal(MotionAvailability.HeldUnknown,atFailure.MotionAvailability);
        Assert.Equal(MotionAvailability.HeldUnknown,current.MotionAvailability);
        Assert.Equal(10000,(window.DeadlineUtc-window.StartedUtc).TotalMilliseconds);
        Assert.True(window.StartTick>=intentTick);
        Assert.True(Stopwatch.GetTimestamp()>=window.DueTick);
        Assert.Contains(events,x=>x.GetProperty("outcome").GetString()=="ClosedWithoutAuthorization");
    }
    internal static void AssertStillClosed(DeviceObservation current)
    {
        Assert.True(current.HasReliableObservation);
        Assert.Equal(MotionAvailability.HeldUnknown,current.MotionAvailability);
    }
    internal static void AssertNoContinuationSave(IEnumerable<string> payloads,int handoffCount,string intentPayload,bool capacity)
    {
        Assert.DoesNotContain(payloads,p=>JsonDocument.Parse(p).RootElement.TryGetProperty("kind",out var kind)&&kind.GetString()=="RecipePlanBound");
        Assert.Equal(0,handoffCount);
        using var input=JsonDocument.Parse(intentPayload);
        Assert.Equal(capacity,input.RootElement.GetProperty("ngCapacity").ValueKind!=JsonValueKind.Null);
        Assert.Equal(capacity,input.RootElement.GetProperty("pendingCapacity").ValueKind!=JsonValueKind.Null);
    }
}
