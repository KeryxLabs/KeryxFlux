using System.Reflection;
using System.Runtime.Loader;

namespace KeryxFlux.Application.FileSystem
{
    internal class LibraryLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;
        public LibraryLoadContext(string libraryPath)
        {
            _resolver = new AssemblyDependencyResolver(libraryPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);

            if (assemblyPath != null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        }

        protected override nint LoadUnmanagedDll(string unmanagedDllName)
        {
            string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);

            if (libraryPath != null)
            {

                return base.LoadUnmanagedDll(unmanagedDllName);
            }
            return nint.Zero;
        }
    }
}
