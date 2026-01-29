namespace KeryxFlux.Domain.Abstractions
{
    public record Error(string Code, string? Details = null)
    {
        public static Error None => new(string.Empty);
        public static Error NullValue => new(nameof(NullValue));
    }
}
