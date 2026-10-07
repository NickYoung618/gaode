using Gaode.Application.Ports;

namespace Gaode.Host.Api;

public static class MediaEndpoints
{
    public static RouteGroupBuilder MapMediaEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/media/{mediaId:guid}", async (Guid mediaId, IMediaStore media,
            Gaode.Infrastructure.Media.MediaStore catalog, HttpContext http, CancellationToken ct) =>
        {
            if (!catalog.TryGetReference(mediaId, out var value) || !media.IsReady(mediaId))
                return Station01ApiResults.NotFound(http, "MediaNotFound", "媒体不存在或尚未保存");
            var reference = value;
            http.Response.Headers.ETag = reference.ETag;
            if (http.Request.Headers.IfNoneMatch.Any(x => string.Equals(x, reference.ETag, StringComparison.Ordinal)))
                return Results.StatusCode(StatusCodes.Status304NotModified);
            var stream = await media.OpenReadAsync(mediaId, ct);
            return Results.Stream(stream, reference.ContentType);
        }).RequireAuthorization(Station01Authorization.MediaRead);
        return group;
    }
}
