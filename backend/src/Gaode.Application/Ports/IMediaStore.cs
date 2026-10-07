namespace Gaode.Application.Ports;

public interface IMediaStore
{
    IDisposable ReserveCapture(Guid captureId, string role, long maxBytes);
    ValueTask<MediaRef> SaveAsync(Guid runId, Guid captureId, string role,
        string pointVersion, string scopeVersion, byte[] buffer, string format,
        string source, CancellationToken cancellationToken);
    ValueTask<Stream> OpenReadAsync(Guid mediaId, CancellationToken cancellationToken);
    IDisposable Lease(Guid mediaId, string consumer);
    bool IsReady(Guid mediaId);
}
