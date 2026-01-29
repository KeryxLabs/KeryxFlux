using KeryxFlux.Domain.Models;

namespace KeryxFlux.Domain.Requesting
{
    internal interface IHttpCacheClient
    {
        void SaveToken(string key, string token);
        string? GetToken(string key);
        Task SaveTokenAsync(string key, string token, CancellationToken cancellationToken = default) => Task.Run(() => SaveToken(key, token), cancellationToken);
        Task<string?> GetTokenAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(GetToken(key));

        void SaveProcessingTime(string key, EntryProcessTime processTime);
        Task SaveProcessingTimeAsync(string key, EntryProcessTime processTime, CancellationToken cancellationToken = default) =>
            Task.Run(() => SaveProcessingTime(key, processTime), cancellationToken);

        EntryProcessTime GetProcessingTime(string key);
        Task<EntryProcessTime> GetProcessingTimeAsync(string key, CancellationToken cancellationToken = default) => 
            Task.FromResult(GetProcessingTime(key));
    }
}
