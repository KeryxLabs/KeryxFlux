using KeryxFlux.Domain.Models.Http;

namespace KeryxFlux.Domain.Abstractions
{
    public interface IHttpReqStr
    {
        IAsyncEnumerable<byte[]> ConsumeHttpRequest(HttpServerRequest request, CancellationToken cancellationToken = default);
    }
}
