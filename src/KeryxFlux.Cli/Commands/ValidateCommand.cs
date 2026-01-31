using Cocona;
using KeryxFlux.Domain.Models;
using Spectre.Console;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace KeryxFlux.Cli.Commands;

public static class ValidateCommand
{
    public static async Task Execute(
        [Argument(Description = "Path to docket file or directory")] string path,
        [Option('v', Description = "Verbose output")] bool verbose = false,
        IAnsiConsole console = default!)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            console.MarkupLine($"[red]Error:[/] Path not found: {path}");
            Environment.Exit(1);
        }

        var files = GetDocketFiles(path);
        
        if (!files.Any())
        {
            console.MarkupLine($"[yellow]Warning:[/] No docket files found at {path}");
            Environment.Exit(0);
        }

        await console.Status()
            .StartAsync("Validating dockets...", async ctx =>
            {
                var results = new List<ValidationResult>();

                foreach (var file in files)
                {
                    ctx.Status($"Validating {Path.GetFileName(file)}...");
                    var result = await ValidateDocket(file, verbose);
                    results.Add(result);
                }

                DisplayResults(console, results, verbose);
            });
    }

    private static List<string> GetDocketFiles(string path)
    {
        if (File.Exists(path))
        {
            return new List<string> { path };
        }

        return Directory.GetFiles(path, "*.yaml", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(path, "*.yml", SearchOption.AllDirectories))
            .ToList();
    }

    private static async Task<ValidationResult> ValidateDocket(string filePath, bool verbose)
    {
        var result = new ValidationResult { FilePath = filePath };

        try
        {
            var yaml = await File.ReadAllTextAsync(filePath);
            
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var docket = deserializer.Deserialize<Docket>(yaml);

            ValidateDocketStructure(docket, result);
            ValidateDateVariables(docket, result);
            ValidateMultiTenantConfiguration(docket, result);

            result.IsValid = !result.Errors.Any();
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"Failed to parse YAML: {ex.Message}");
        }

        return result;
    }

    private static void ValidateDocketStructure(Docket docket, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(docket.Name))
            result.Errors.Add("Docket name is required");

        if (string.IsNullOrWhiteSpace(docket.Version))
            result.Errors.Add("Docket version is required");

        if (docket.Scheduler == null)
            result.Errors.Add("Scheduler configuration is required");
    }

    private static void ValidateDateVariables(Docket docket, ValidationResult result)
    {
        if (docket.DateVariables == null || !docket.DateVariables.Any())
            return;

        var variableNames = new HashSet<string>();

        foreach (var dateVar in docket.DateVariables)
        {
            if (string.IsNullOrWhiteSpace(dateVar.Name))
            {
                result.Errors.Add("Date variable name is required");
                continue;
            }

            if (!variableNames.Add(dateVar.Name))
            {
                result.Errors.Add($"Duplicate date variable name: {dateVar.Name}");
            }

            if (string.IsNullOrWhiteSpace(dateVar.OffsetExpression))
            {
                result.Errors.Add($"Date variable '{dateVar.Name}' missing offset_expression");
            }

            if (string.IsNullOrWhiteSpace(dateVar.Format))
            {
                result.Warnings.Add($"Date variable '{dateVar.Name}' missing format, will use default");
            }
        }
    }

    private static void ValidateMultiTenantConfiguration(Docket docket, ValidationResult result)
    {
        if (docket.Tenants == null || !docket.Tenants.Any())
            return;

        var tenantIds = new HashSet<string>();

        foreach (var tenant in docket.Tenants)
        {
            if (string.IsNullOrWhiteSpace(tenant.TenantId))
            {
                result.Errors.Add("Tenant ID is required");
                continue;
            }

            if (!tenantIds.Add(tenant.TenantId))
            {
                result.Errors.Add($"Duplicate tenant ID: {tenant.TenantId}");
            }
        }

        if (docket.Endpoints == null || !docket.Endpoints.Any())
        {
            result.Warnings.Add("Multi-tenant configuration present but no endpoints defined");
        }
    }

    private static void DisplayResults(IAnsiConsole console, List<ValidationResult> results, bool verbose)
    {
        var totalFiles = results.Count;
        var validFiles = results.Count(r => r.IsValid);
        var invalidFiles = results.Count(r => !r.IsValid);
        var totalWarnings = results.Sum(r => r.Warnings.Count);

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("File")
            .AddColumn("Status")
            .AddColumn("Issues");

        foreach (var result in results)
        {
            var fileName = Path.GetFileName(result.FilePath);
            var status = result.IsValid ? "[green]:check_mark: Valid[/]" : "[red]:cross_mark: Invalid[/]";
            var issues = result.Errors.Any() 
                ? $"[red]{result.Errors.Count} error(s)[/]" 
                : result.Warnings.Any() 
                    ? $"[yellow]{result.Warnings.Count} warning(s)[/]" 
                    : "[green]None[/]";

            table.AddRow(fileName, status, issues);
        }

        console.Write(table);

        if (verbose)
        {
            foreach (var result in results.Where(r => !r.IsValid || r.Warnings.Any()))
            {
                console.WriteLine();
                console.MarkupLine($"[bold]{Path.GetFileName(result.FilePath)}[/]");

                if (result.Errors.Any())
                {
                    console.MarkupLine("[red]Errors:[/]");
                    foreach (var error in result.Errors)
                    {
                        console.MarkupLine($"  [red]:cross_mark:[/] {error}");
                    }
                }

                if (result.Warnings.Any())
                {
                    console.MarkupLine("[yellow]Warnings:[/]");
                    foreach (var warning in result.Warnings)
                    {
                        console.MarkupLine($"  [yellow]:warning:[/] {warning}");
                    }
                }
            }
        }

        console.WriteLine();
        var summaryColor = invalidFiles > 0 ? "red" : totalWarnings > 0 ? "yellow" : "green";
        console.MarkupLine($"[{summaryColor}]Summary:[/] {validFiles}/{totalFiles} valid, {invalidFiles} invalid, {totalWarnings} warnings");

        Environment.Exit(invalidFiles > 0 ? 1 : 0);
    }

    private class ValidationResult
    {
        public string FilePath { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }
}
