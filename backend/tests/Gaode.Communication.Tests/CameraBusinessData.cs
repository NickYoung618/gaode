using System.Text.Json;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;

namespace Gaode.Communication.Tests;

// Read-only historical Test input snapshots, copied into this test's fixture directory.
internal static class ReviewBusinessData
{
    public static T Read<T>(string name) => JsonSerializer.Deserialize<T>(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory,"Fixtures","CameraReview",name)),new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    public static BusinessBudget Budget()=>Read<BusinessBudget>("budgets.test.json");
    public static PublicConfiguration Motion()
    {
        var source=Read<PublicConfiguration>("public.test.json");
        return source with {Motion=source.Motion with {Frame="test-frame",CoordinateSource="Test:Recipe011Data explicit component coordinates"}};
    }
}
internal static class SemanticDeviceFixture
{
    public static DeviceObservation Ready(ClampState clamp=ClampState.Secured)
    {
        var now=DateTimeOffset.UtcNow;
        return new(DeviceReliability.Reliable,DeviceConnection.Connected,1,OperatingMode.Automatic,
            DeviceReadiness.Ready,SafetyAssessment.Clear,clamp,MotionAvailability.Available,
            AcquisitionReadiness.Available,ManualAreaState.Clear,ManualHandlingState.Unconfirmed,
            null,null,[],[],new(DeviceProvider.Simulated,"SemanticUnitFixture/1",EvidenceQuality.Derived),
            new(Guid.NewGuid(),1,now,now,DeviceReliability.Reliable));
    }
}
