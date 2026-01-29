using KeryxFlux.Domain.Abstractions;


namespace KeryxFlux.Domain
{
    public record LoadingError(string Code, string? Details = null) : Error(Code, Details)
    {
        public static LoadingError EmptyAssemblyName => new(nameof(EmptyAssemblyName), "Loaded AssemblyName did not contain a valid name.");
        public static LoadingError EmptyVersion => new(nameof(EmptyVersion), "Loaded Version was emtpy.");
        public static LoadingError VersionNotParsable => new(nameof(VersionNotParsable), "Loaded Version was not parsable.");
        public static LoadingError NullPlugin => new(nameof(NullPlugin), "Plugin was not found.");
        public static LoadingError NonReadableFile => new(nameof(NonReadableFile), "Unable to read the text file.");
        public static LoadingError InvalidYmlFile => new(nameof(InvalidYmlFile), "File provided was not of extension [.yml,.yaml].");
        public static LoadingError UnableToLoadDocket(string fileName) => new(nameof(UnableToLoadDocket), $"Docket file: {fileName} was not able to be loaded.");
        public static LoadingError UnableToUnloadDocket(string fileName) => new(nameof(UnableToUnloadDocket), $"Docket file: {fileName} was not able to be unloaded.");
        public static LoadingError UnableToLoadDocketFile => new(nameof(UnableToLoadDocketFile), "Docket file was not able to be loaded.");
        public static LoadingError UnableToUnloadDocketFile => new(nameof(UnableToUnloadDocketFile), "Docket file was not able to be unloaded.");
    }
}
