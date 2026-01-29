namespace KeryxFlux.Domain.Models
{
    public readonly record struct RequestUrl(string Value)
    {
        public RequestUrl() : this(string.Empty) { }
        public static RequestUrl Empty => new(string.Empty);
        public static implicit operator string(RequestUrl request) => request.Value;
    }
}
