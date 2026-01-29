
using KeryxFlux.Domain.Abstractions;

namespace KeryxFlux.Domain
{
    public record ProcessingError(string Code, string? Details = null): Error(Code, Details)
    {
        public static ProcessingError HttpRequestFailure(string code, string details) => new(code, details);
        public static ProcessingError EmptyAuthToken => new(nameof(EmptyAuthToken));
        public static ProcessingError InvalidAuthToken(string? message = null) => new(nameof(InvalidAuthToken), message);
    }
}
