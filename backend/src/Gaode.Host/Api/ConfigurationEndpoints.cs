using Gaode.Application.Configuration;
using Gaode.Application.Capabilities;
using Gaode.Application.Ports;
using Gaode.Host.Composition;
using Gaode.Domain.Configuration;
using Gaode.Infrastructure.Diagnostics;

namespace Gaode.Host.Api;

public sealed record PublicConfigValidateRequest(ConfigReference PublicConfigRef,
    ConfigReference BudgetRef, ConfigReference? SimulationRef);
public sealed record SavePublicPositionsRequest(FixedPoint ThreeD, FixedPoint ManualLoading, string ExpectedDigest);

public static class ConfigurationEndpoints
{
    public static RouteGroupBuilder MapConfigurationEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/configuration/public-positions", (PublicPositionTeaching teaching,
            Station01RuntimeOptions options) => {
                var config = teaching.Read(options.PublicReference);
                return Results.Ok(new { config.Digest, threeD = config.Value.Motion.Points.ThreeD,
                    manualLoading = config.Value.Motion.Points.Unload });
            }).RequireAuthorization(Station01Authorization.Read);
        group.MapGet("/configuration/public-positions/current", (string kind, PublicPositionTeaching teaching,
            Station01RuntimeOptions options, HttpContext http, StructuredStageDiagnostics diagnostics) => {
                try { return Results.Ok(teaching.ReadCurrent(options.PublicReference, kind)); }
                catch (Exception error) when (error is ArgumentException or InvalidOperationException or ConfigurationException)
                { diagnostics.Record(http.TraceIdentifier,null,null,"PublicPositions",error is ConfigurationException c ? c.Code : "TeachingRejected","NoCandidate_NoMotion",exception:error);
                  return Station01ErrorMapping.Map(http, error); }
            }).RequireAuthorization(Station01Authorization.ConfigWrite);
        group.MapPost("/configuration/public-positions", (SavePublicPositionsRequest request,
            PublicPositionTeaching teaching, Station01RuntimeOptions options, HttpContext http, StructuredStageDiagnostics diagnostics) => {
                try {
                    var saved = teaching.Save(options.PublicReference, request.ThreeD, request.ManualLoading,
                        request.ExpectedDigest, http.User.Identity?.Name ?? http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
                    return Results.Ok(new { saved.Digest, threeD = saved.Value.Motion.Points.ThreeD,
                        manualLoading = saved.Value.Motion.Points.Unload });
                }
                catch (ConfigurationException error) when (error.Code == "PublicPositionRevisionConflict")
                { diagnostics.Record(http.TraceIdentifier,null,null,"PublicPositions",error.Code,"RereadBeforeSave");
                  return Results.Conflict(new { code = error.Code, message = error.Message }); }
                catch (Exception error) when (error is ArgumentException or InvalidOperationException or ConfigurationException)
                { diagnostics.Record(http.TraceIdentifier,null,null,"PublicPositions",error is ConfigurationException c ? c.Code : "SaveRejected","NoNewConfigurationClaim",exception:error);
                  return Station01ErrorMapping.Map(http, error); }
            }).RequireAuthorization(Station01Authorization.ConfigWrite);
        group.MapPost("/public-config/validate", (PublicConfigValidateRequest request,
            IPublicConfiguration configurations, PublicConfigurationValidator validator,
            CapabilityRegistry capabilities, Station01RuntimeOptions options, HttpContext http) =>
        {
            try
            {
                var config = configurations.LoadPublic(request.PublicConfigRef);
                var budget = configurations.LoadBudget(request.BudgetRef);
                var simulation = request.SimulationRef is { } sim
                    ? configurations.LoadSimulation(sim).Value : null;
                var result = validator.Validate(config.Value, budget.Value, simulation,
                    options.Mode == "FullSimulation", options.Mode == "VirtualPlcIntegration", options.PlcProvider);
                return Results.Ok(new
                {
                    publicConfig = new { request.PublicConfigRef.Id, request.PublicConfigRef.Version },
                    budget = new { request.BudgetRef.Id, request.BudgetRef.Version },
                    blockingControlErrors = result.BlockingControlErrors,
                    algorithmIssues = result.AlgorithmIssues,
                    warnings = result.Warnings,
                    capabilityVersions = capabilities.Versions
                });
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException)
            { return Station01ErrorMapping.Map(http, error); }
        }).RequireAuthorization(Station01Authorization.ConfigValidate);
        return group;
    }
}
