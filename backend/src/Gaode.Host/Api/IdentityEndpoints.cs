using System.Security.Claims;
namespace Gaode.Host.Api;
public static class IdentityEndpoints
{
    public static RouteGroupBuilder MapIdentityEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/identity",(ClaimsPrincipal user, IConfiguration config, HttpContext http, ILoggerFactory logs)=> {
            var commissioning=config["Gaode:Mode"]=="RealDeviceCommissioning";
            http.Response.Headers.CacheControl="no-store";
            logs.CreateLogger("CommissioningIdentity").LogInformation("IdentityChecked {SubjectId} {ProfileId} {Mode} {TraceId}", user.FindFirstValue(ClaimTypes.NameIdentifier), user.FindFirstValue("profileId"), commissioning ? "RealDeviceCommissioning" : "Test", http.TraceIdentifier);
            return Results.Ok(new { schemaVersion="station01-identity/1", profileId=user.FindFirstValue("profileId"),
                subjectId=user.FindFirstValue(ClaimTypes.NameIdentifier),displayName=user.FindFirstValue(ClaimTypes.Name)??user.FindFirstValue(ClaimTypes.NameIdentifier),
                role=user.FindFirstValue(ClaimTypes.Role),permissions=user.FindAll("permission").Select(c=>c.Value).ToArray(),
                mode=commissioning?"RealDeviceCommissioning":"Test",purpose=commissioning?"Commissioning":"Test",
                authenticationSource=commissioning?"PreconfiguredCommissioning":"ExistingTest" });
        }).RequireAuthorization();
        return group;
    }
}
