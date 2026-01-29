using OneOf;
using KeryxFlux.Domain.Models;

namespace KeryxFlux.Domain.Abstractions
{
    public interface IReqStrAdapter
    {
        public string Name { get; }

        public Result<OneOf<Step, List<Step>>> Process(byte[] source, Action<IParserConfigurator> cfg);
        public Result<Step> Extend(byte[] source, Step previousStep, Action<IParserConfigurator> cfg);

    }
}
