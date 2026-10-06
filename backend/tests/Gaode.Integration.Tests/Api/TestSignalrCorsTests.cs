
using System.Net;
using Gaode.Integration.Tests.Support;
using Xunit;

namespace Gaode.Integration.Tests.Api;

public sealed class TestSignalrCorsTests
{
    private const string Origin = "https://appassets.local";
    private const string Hub = "/hubs/station01";

    [Fact]
    public async Task ControlledTestOriginSupportsLongPollingAndStillRequiresAuthorization()
    {
        await using var plc = await VirtualPlcFixture.CreateAsync(profile: "signalr");
        var port = plc.Port;
        await using var fixture = await Station01HostFixture.CreateAsync(
            mode: "VirtualPlcIntegration", simulationId: "s01-sim-virtual-plc",
            publicId: "s01-public-virtual", budgetId: "s01-budget-virtual-plc",
            plcPort: port, testAllowedOrigin: Origin);

        foreach (var method in new[] { "POST", "GET", "DELETE" })
        {
            using var preflight = new HttpRequestMessage(HttpMethod.Options, Hub + "/negotiate?negotiateVersion=1");
            preflight.Headers.Add("Origin", Origin);
            preflight.Headers.Add("Access-Control-Request-Method", method);
            preflight.Headers.Add("Access-Control-Request-Headers",
                "authorization,content-type,x-requested-with,x-signalr-user-agent");
            using var response = await fixture.Client.SendAsync(preflight);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal(Origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
            Assert.Contains(method, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Methods")));
        }

        using (var otherOrigin = new HttpRequestMessage(HttpMethod.Options, Hub + "/negotiate?negotiateVersion=1"))
        {
            otherOrigin.Headers.Add("Origin", "https://unapproved.example");
            otherOrigin.Headers.Add("Access-Control-Request-Method", "POST");
            using var response = await fixture.Client.SendAsync(otherOrigin);
            Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        }

        using var anonymous = new HttpRequestMessage(HttpMethod.Post, Hub + "/negotiate?negotiateVersion=1");
        anonymous.Headers.Add("Origin", Origin);
        using var unauthenticated = new HttpClient(fixture.Host.Server.CreateHandler())
        { BaseAddress = fixture.Client.BaseAddress };
        using var denied = await unauthenticated.SendAsync(anonymous);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);

        using var authorized = new HttpRequestMessage(HttpMethod.Post, Hub + "/negotiate?negotiateVersion=1");
        authorized.Headers.Add("Origin", Origin);
        using var allowed = await fixture.Client.SendAsync(authorized);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(Origin, Assert.Single(allowed.Headers.GetValues("Access-Control-Allow-Origin")));
    }
}
