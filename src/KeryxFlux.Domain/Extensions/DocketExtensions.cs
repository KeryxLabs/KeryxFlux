
using KeryxFlux.Domain.Models;
using System.Diagnostics.CodeAnalysis;

namespace KeryxFlux.Domain.Extensions
{
    public static class DocketExtensions
    {
        public static bool IsRetriableError(this List<RetriableError> errors, string content, [NotNullWhen(true)]out RetriableError? error)
        {
            error = errors.Where(e => e.Content.Equals(content, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
            return error is not null;
        }
    }
}
