using KeryxFlux.Domain;
using Microsoft.Extensions.Logging;
using KeryxFlux.Domain.Abstractions;
using KeryxFlux.Domain.Models;
using System.Reflection;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace KeryxFlux.Application.FileSystem
{
    public static class Loader
    {
        public static bool VersionChanged(LibraryPath path, Version currentVersion)
        {
            AssemblyName assemblyName = new(Path.GetFileNameWithoutExtension(path));
            return currentVersion != assemblyName.Version;
        }

        public static Result<LibraryMetadata> LoadFromPath(LibraryPath path)
        {
            LibraryLoadContext loadContext = new(path);
            AssemblyName assemblyName = new(Path.GetFileNameWithoutExtension(path));

            if (assemblyName.Name is null)
                return LoadingError.EmptyAssemblyName;

            Assembly assembly = loadContext.LoadFromAssemblyName(assemblyName);

            var libType = assembly.GetTypes().Where(t => typeof(IReqStrAdapter).IsAssignableFrom(t)).Take(1).FirstOrDefault();
            var versionStr = assembly.FullName?.Split(',').ElementAtOrDefault(1)?.Split('=').ElementAtOrDefault(1) ?? string.Empty;

            if (string.IsNullOrEmpty(versionStr)) return LoadingError.EmptyVersion;
            if (!Version.TryParse(versionStr, out var version)) return LoadingError.VersionNotParsable;
            if (libType == null) return LoadingError.NullPlugin;

            var info = new LibraryInfo()
            {
                LibraryPath = path,
                LibraryName = new(assemblyName.Name),
                LibraryVersion = version,
            };

            return new LibraryMetadata
            {
                Info = info,
                Type = libType,
            };
        }
        public static Result<Docket> LoadDocket(DocketPath docketPath)
        {
            try
            {
                var de = new DeserializerBuilder()
                    .WithNamingConvention(UnderscoredNamingConvention.Instance)
                    .WithEnumNamingConvention(UnderscoredNamingConvention.Instance)
                    .Build();
                var loadedStr = LoadYaml(docketPath);

                if (loadedStr.IsFailure)
                    return loadedStr.Error;
                Docket docket = de.Deserialize<Docket>(loadedStr.Value!);

                return docket;

            }
            catch (Exception)
            {
                return LoadingError.InvalidYmlFile;
            }
        }
        public static IEnumerable<Docket> LoadDockets(string rootPath)
        {
            ArgumentNullException.ThrowIfNull(rootPath);
            try
            {
                var de = new DeserializerBuilder()
                                    .WithNamingConvention(UnderscoredNamingConvention.Instance)
                                    .WithEnumNamingConvention(UnderscoredNamingConvention.Instance)
                                    .Build();
                return LoadYamlFilesFromDirectory(rootPath).Select(de.Deserialize<Docket>);
            }
            catch (Exception)
            {
                throw;
            }


        }

        private static IEnumerable<string> LoadYamlFilesFromDirectory(string root)
        {
            if (Directory.Exists(root))
            {
                return Directory.EnumerateFiles(root)
                                        .Select(LoadYaml)
                                        .Where(r => r.IsSuccess)
                                        .Select(r => r.Value!);

            }
            else
                throw new Exception("Yaml Directory does not exist");
        }



        private static Result<string> LoadYaml(string path)
        {
            var pathToCheck = path.ToLower();
            if (pathToCheck.EndsWith(".yaml") || pathToCheck.EndsWith(".yml"))
            {
                try
                {

                    return File.ReadAllText(path);
                }
                catch (Exception)
                {
                    return LoadingError.NonReadableFile;
                }

            }
            else
                return LoadingError.InvalidYmlFile;
        }

    }
}

