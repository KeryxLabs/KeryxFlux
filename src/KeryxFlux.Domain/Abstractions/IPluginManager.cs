using KeryxFlux.Domain.Models;
using System.Diagnostics.CodeAnalysis;

namespace KeryxFlux.Domain.Abstractions
{
    public interface IPluginManager
    {
        bool TryGetInstance(LibraryMetadata metadata, [NotNullWhen(true)] out IReqStrAdapter? plugin);
        bool TryGetMetadata(LibraryPath libraryPath, [NotNullWhen(true)] out LibraryMetadata? metadata);
        bool TryLoad(LibraryPath libraryPath, [NotNullWhen(true)] out LibraryMetadata? metadata);
        bool TryUnload(LibraryMetadata metadata);
    }
}
