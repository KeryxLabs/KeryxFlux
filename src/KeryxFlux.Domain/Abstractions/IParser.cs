namespace KeryxFlux.Domain.Abstractions
{
    public interface IParser<TDestination>
    {
        Result<TDestination> Parse(byte[] source);
    }
}
