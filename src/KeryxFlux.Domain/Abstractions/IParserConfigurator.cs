using KeryxFlux.Domain.Models;

namespace KeryxFlux.Domain.Abstractions
{
    public interface IParserConfigurator
    {
        public void Configure(ParserConfig config);
    }
}
