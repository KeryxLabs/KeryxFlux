using KeryxFlux.Domain.Abstractions;


namespace KeryxFlux.Domain.Models
{
    public readonly record struct Step(string Name,RequestUrl Request, Interpreted State)
    {
        public static Step Empty(Interpreted state) => new(string.Empty, RequestUrl.Empty, state);
        public bool IsNone => Request != RequestUrl.Empty;
    }
}
