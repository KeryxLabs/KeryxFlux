namespace KeryxFlux.Domain.Models
{
    public readonly record struct DocketPath(string Value)
    {
        private static readonly string[] ValidationEndings = [".yml", ".yaml"];

        public string Value { get; init; } = string.IsNullOrEmpty(Value) ?
                                                throw new ArgumentException("Value cannot be null or empty.", nameof(Value))
                                                : !ValidationEndings.Any(Value.Trim().EndsWith) ?
                                                    throw new ArgumentException("Value does not follow expected naming convention.", nameof(Value))
                                                    : Value;
        public DocketPath() : this(string.Empty) { }

        public static implicit operator string(DocketPath docketPath) => docketPath.Value;

    }
}
