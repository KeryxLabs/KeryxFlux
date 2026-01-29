
namespace KeryxFlux.Domain.Abstractions
{
    public interface IRunnable<E>
        where E : RunnableEntry
    {
        public Task ProcessEntryAsync(E entry, CancellationToken cancellationToken = default);
    }
}
