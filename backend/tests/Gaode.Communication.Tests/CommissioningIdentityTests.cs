using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Gaode.Host.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace Gaode.Communication.Tests;
public sealed class CommissioningIdentityTests
{
    [Theory]
    [InlineData("Operator", true, false)]
    [InlineData("ProcessEngineer", false, true)]
    public async Task BackendConfirmsIdentityAndEnforcesPermissions(string role, bool canRun, bool canEdit)
    {
        var token = Guid.NewGuid().ToString("N");
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["Gaode:Mode"] = "RealDeviceCommissioning";
        var section = "Gaode:CommissioningIdentities:0:";
        foreach (var pair in new Dictionary<string,string> { ["ProfileId"]="offline-"+role, ["SubjectId"]="offline:"+role,
            ["DisplayName"]="OFFLINE "+role, ["Role"]=role, ["Purpose"]="RealDeviceCommissioning", ["Credential"]=token })
            builder.Configuration[section+pair.Key]=pair.Value;
        builder.Services.AddStation01Api(builder.Configuration);
        await using var app=builder.Build(); app.UseCors("Station01CommissioningPage"); app.UseAuthentication(); app.UseAuthorization();
        app.MapGroup("/api/v1/station01").MapIdentityEndpoints();
        app.MapPost("/run",()=>Results.Ok()).RequireAuthorization(Station01Authorization.Start);
        app.MapPost("/edit",()=>Results.Ok()).RequireAuthorization(Station01Authorization.RecipeWrite);
        app.Urls.Add("http://127.0.0.1:0"); await app.StartAsync();
        try {
            using var client=new HttpClient { BaseAddress=new Uri(app.Urls.Single()) };
            Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/v1/station01/identity")).StatusCode);
            client.DefaultRequestHeaders.Authorization=new("Bearer",token);
            using var response=await client.GetAsync("/api/v1/station01/identity");
            Assert.Equal(HttpStatusCode.OK,response.StatusCode); Assert.True(response.Headers.CacheControl!.NoStore);
            var body=await response.Content.ReadAsStringAsync(); Assert.DoesNotContain(token,body);
            using var identity=JsonDocument.Parse(body);
            Assert.Equal("offline:"+role,identity.RootElement.GetProperty("subjectId").GetString());
            Assert.Equal("Commissioning",identity.RootElement.GetProperty("purpose").GetString());
            Assert.Equal(canRun?HttpStatusCode.OK:HttpStatusCode.Forbidden,(await client.PostAsync("/run",null)).StatusCode);
            Assert.Equal(canEdit?HttpStatusCode.OK:HttpStatusCode.Forbidden,(await client.PostAsync("/edit",null)).StatusCode);
            using var allowedCors = new HttpRequestMessage(HttpMethod.Options, "/api/v1/station01/identity");
            allowedCors.Headers.Add("Origin", "https://appassets.local");
            allowedCors.Headers.Add("Access-Control-Request-Method", "GET");
            allowedCors.Headers.Add("Access-Control-Request-Headers", "authorization");
            using var preflight = await client.SendAsync(allowedCors);
            Assert.Equal("https://appassets.local", Assert.Single(preflight.Headers.GetValues("Access-Control-Allow-Origin")));
            using var deniedCors = new HttpRequestMessage(HttpMethod.Options, "/api/v1/station01/identity");
            deniedCors.Headers.Add("Origin", "https://untrusted.invalid");
            deniedCors.Headers.Add("Access-Control-Request-Method", "GET");
            using var deniedPreflight = await client.SendAsync(deniedCors);
            Assert.False(deniedPreflight.Headers.Contains("Access-Control-Allow-Origin"));
            client.DefaultRequestHeaders.Authorization=new("Bearer","invalid");
            Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/v1/station01/identity")).StatusCode);
            client.DefaultRequestHeaders.Authorization=null;
            Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/v1/station01/identity?access_token="+token)).StatusCode);
        } finally { await app.StopAsync(); }
    }
    [Theory]
    [InlineData("SystemAdministrator")]
    [InlineData("EquipmentEngineer")]
    [InlineData("Operator")]
    public void MissingOrDisallowedIdentityConfigurationFailsWithoutTestFallback(string role)
    {
        var b=WebApplication.CreateBuilder(); b.Configuration["Gaode:Mode"]="RealDeviceCommissioning";
        b.Configuration["Gaode:Tokens:Operator"]="old-test";
        b.Configuration["Gaode:CommissioningIdentities:0:Role"]=role;
        Assert.Throws<InvalidOperationException>(()=>b.Services.AddStation01Api(b.Configuration));
    }
    [Theory]
    [InlineData("duplicate")]
    [InlineData("TestCredential")]
    [InlineData("TestPurpose")]
    public void CompleteButConflictingIdentityConfigurationIsRejected(string invalid)
    {
        var b = WebApplication.CreateBuilder(); b.Configuration["Gaode:Mode"] = "RealDeviceCommissioning";
        var token = Guid.NewGuid().ToString("N");
        foreach (var i in new[] { 0, 1 })
        {
            var p = "Gaode:CommissioningIdentities:" + i + ":";
            foreach (var pair in new Dictionary<string,string> { ["ProfileId"] = "offline-" + i, ["SubjectId"] = "offline:" + i,
                ["DisplayName"] = "OFFLINE " + i, ["Role"] = "Operator", ["Purpose"] = "RealDeviceCommissioning", ["Credential"] = i == 0 ? token : Guid.NewGuid().ToString("N") }) b.Configuration[p + pair.Key] = pair.Value;
        }
        if (invalid == "duplicate") b.Configuration["Gaode:CommissioningIdentities:1:Credential"] = token;
        if (invalid == "TestCredential") b.Configuration["Gaode:Tokens:Operator"] = token;
        if (invalid == "TestPurpose") b.Configuration["Gaode:CommissioningIdentities:1:Purpose"] = "Test";
        Assert.Throws<InvalidOperationException>(() => b.Services.AddStation01Api(b.Configuration));
    }
}
