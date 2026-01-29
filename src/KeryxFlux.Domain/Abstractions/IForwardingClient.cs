
namespace KeryxFlux.Domain.Abstractions
{
    public interface IForwardingClient
    {
        public Task Send(Interpreted source, CancellationToken cancellationToken = default);
    }
}
