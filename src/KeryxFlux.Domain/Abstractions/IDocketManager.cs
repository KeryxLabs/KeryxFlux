using KeryxFlux.Domain.Models;
using System.Diagnostics.CodeAnalysis;

namespace KeryxFlux.Domain.Abstractions
{
    public interface IDocketManager
    {
        bool TryLoad(DocketPath docketPath, [NotNullWhen(true)] out Docket? docket);
        bool TryUnload(DocketPath docketPath, [NotNullWhen(true)] out Docket? docket);
        bool TryGetPluginType(string docketName, [NotNullWhen(true)] out Type? type);
        bool TryGetDocket(string docketName, [NotNullWhen(true)] out Docket? docket);
        bool TryGetPluginInstance(string docketName, [NotNullWhen(true)] out IReqStrAdapter? plugin);
        IEnumerable<Docket> GetLoadedDockets();

    }
}
