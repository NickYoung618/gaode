using Gaode.Host.Composition;
using Gaode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Gaode.Host.Api;

// Finite, authorized read-only diagnostic route. No business decisions or wire decoding.
public static class CommunicationDiagnosticEndpoints
{
    public static RouteGroupBuilder MapCommunicationDiagnostics(this RouteGroupBuilder group)
    {
        group.MapGet("/diagnostics/communication/{evidenceId:guid}", async (Guid evidenceId,
            DbContextOptions<Station01DbContext> options, Station01RuntimeOptions runtime,
            HttpContext http, CancellationToken token) =>
        {
            var store = StoreCompatibilityProbe.Inspect(runtime.TestRoot, runtime.StoreProfile);
            if (!store.Compatible || store.StoreId is not { } storeId)
                return Station01ApiResults.Error(http, StatusCodes.Status503ServiceUnavailable,
                    "DiagnosticStoreUnavailable", "通信证据存储当前不可读取", "Storage");
            CommunicationEvidenceDocument? document;
            try { document = await new CommunicationEvidenceReader(options, storeId).ReadAsync(evidenceId, token); }
            catch (InvalidDataException)
            {
                return Station01ApiResults.Error(http, StatusCodes.Status409Conflict,
                    "DiagnosticEvidenceInvalid", "通信证据的身份或完整性核验未通过", "Storage");
            }
            if (document is null)
                return Station01ApiResults.NotFound(http, "CommunicationEvidenceNotFound", "未找到已提交的通信证据");
            var etag = $"\"communication-{document.EvidenceId:N}-{document.PayloadDigest}\"";
            http.Response.Headers.ETag = etag;
            return http.Request.Headers.IfNoneMatch.Any(x => string.Equals(x, etag, StringComparison.Ordinal))
                ? Results.StatusCode(StatusCodes.Status304NotModified) : Results.Ok(document);
        }).RequireAuthorization(Station01Authorization.Read);
        return group;
    }
}
