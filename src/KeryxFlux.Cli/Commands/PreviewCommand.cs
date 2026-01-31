using Cocona;
using KeryxFlux.Domain.Models;
using KeryxFlux.Domain.Models.Dockets;
using KeryxFlux.Domain.Utilities;
using Spectre.Console;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace KeryxFlux.Cli.Commands;

public static class PreviewCommand
{
    public static async Task Execute(
        [Argument(Description = "Path to docket file")] string path,
        [Option("at", Description = "Preview at specific time (ISO 8601)")] string? atTime = null,
        [Option("tenant", Description = "Preview specific tenant only")] string? tenantId = null,
        IAnsiConsole console = default!)
    {

        if (!File.Exists(path))
        {
            console.MarkupLine($"[red]Error:[/] File not found: {path}");
            Environment.Exit(1);
        }

        var baseTime = ParseBaseTime(atTime, console);
        var docket = await LoadDocket(path, console);

        if (docket == null)
        {
            Environment.Exit(1);
        }

        console.MarkupLine($"[bold]Preview for '{docket.Name}'[/] at {baseTime:yyyy-MM-dd HH:mm:ss} UTC");
        console.WriteLine();

        DisplayDateVariables(console, docket.DateVariables, baseTime);
        console.WriteLine();

        if (docket.Tenants != null && docket.Tenants.Any())
        {
            await DisplayMultiTenantExpansion(console, docket, tenantId, baseTime);
        }
        else
        {
            DisplaySingleEndpoint(console, docket, baseTime);
        }
    }

    private static DateTimeOffset ParseBaseTime(string? atTime, IAnsiConsole console)
    {
        if (string.IsNullOrWhiteSpace(atTime))
        {
            return DateTimeOffset.UtcNow;
        }

        if (!DateTimeOffset.TryParse(atTime, out var parsed))
        {
            console.MarkupLine($"[yellow]Warning:[/] Invalid time format '{atTime}', using current time");
            return DateTimeOffset.UtcNow;
        }

        return parsed.ToUniversalTime();
    }

    private static async Task<Docket?> LoadDocket(string path, IAnsiConsole console)
    {
        try
        {
            var yaml = await File.ReadAllTextAsync(path);

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            return deserializer.Deserialize<Docket>(yaml);
        }
        catch (Exception ex)
        {
            console.MarkupLine($"[red]Error:[/] Failed to load docket: {ex.Message}");
            return null;
        }
    }

    private static void DisplayDateVariables(IAnsiConsole console, List<DateVariableConfiguration>? dateVars, DateTimeOffset baseTime)
    {
        if (dateVars == null || !dateVars.Any())
        {
            console.MarkupLine("[dim]No date variables defined[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Variable")
            .AddColumn("Offset")
            .AddColumn("Resolved Value");

        foreach (var dateVar in dateVars)
        {
            var resolvedValue = DateTemplateResolver.ResolveDateVariable(dateVar, baseTime);
            table.AddRow(
                $"[cyan]{{{dateVar.Name}}}[/]",
                dateVar.OffsetExpression ?? "0",
                $"[green]{resolvedValue}[/]"
            );
        }

        console.MarkupLine("[bold]Date Variables:[/]");
        console.Write(table);
    }

    private static async Task DisplayMultiTenantExpansion(IAnsiConsole console, Docket docket, string? filterTenantId, DateTimeOffset baseTime)
    {
        console.MarkupLine("[bold]Multi-Tenant Expansion:[/]");
        console.WriteLine();

        var tenants = filterTenantId != null
            ? docket.Tenants!.Where(t => t.TenantId == filterTenantId).ToList()
            : docket.Tenants!;

        if (filterTenantId != null && !tenants.Any())
        {
            console.MarkupLine($"[yellow]Warning:[/] Tenant '{filterTenantId}' not found");
            return;
        }

        foreach (var tenant in tenants)
        {
            var panel = new Panel(GenerateTenantPreview(docket, tenant, baseTime))
                .Header($"[bold]{tenant.TenantId}[/] - {tenant.DisplayName}")
                .BorderColor(Color.Blue);

            console.Write(panel);
            console.WriteLine();
        }
    }

    private static string GenerateTenantPreview(Docket docket, TenantConfiguration tenant, DateTimeOffset baseTime)
    {
        var mergedDateVars = MergeDateVariables(docket.DateVariables, tenant.DateVariables);
        var staticVars = MergeConfiguration(docket.BaseConfiguration, tenant.Configuration);

        var output = new List<string>();

        if (mergedDateVars.Any())
        {
            output.Add("[dim]Date Variables (tenant-specific):[/]");
            foreach (var dateVar in mergedDateVars)
            {
                var resolved = DateTemplateResolver.ResolveDateVariable(dateVar, baseTime);
                output.Add($"  [cyan]{{{dateVar.Name}}}[/] ? [green]{resolved}[/]");
            }
            output.Add("");
        }

        if (docket.Endpoints != null && docket.Endpoints.Any())
        {
            output.Add("[dim]Endpoints:[/]");
            foreach (var endpoint in docket.Endpoints.Where(e => e.Enabled))
            {
                var endpointConfig = MergeConfiguration(staticVars, endpoint.Configuration);
                var template = endpointConfig.TryGetValue("base_url", out var url) ? url : "No URL configured";
                
                // Convert double curly brackets {{var}} to single {var} for resolver
                var normalizedTemplate = template.Replace("{{", "{").Replace("}}", "}");
                var resolved = PathTemplateResolver.ResolveWithDates(normalizedTemplate, endpointConfig, mergedDateVars, baseTime);
                
                output.Add($"  [yellow]{endpoint.EndpointId}[/]:");
                output.Add($"    {resolved.EscapeMarkup()}");
            }
        }

        return string.Join(Environment.NewLine, output);
    }

    private static void DisplaySingleEndpoint(IAnsiConsole console, Docket docket, DateTimeOffset baseTime)
    {
        if (docket.Scheduler?.Server == null)
        {
            console.MarkupLine("[yellow]No server configuration found[/]");
            return;
        }

        var template = docket.Scheduler.Server.Address ?? "No address configured";
        
        // Convert double curly brackets {{var}} to single {var} for resolver
        var normalizedTemplate = template.Replace("{{", "{").Replace("}}", "}");
        
        var resolved = PathTemplateResolver.ResolveWithDates(
            normalizedTemplate,
            docket.Configuration ?? new Dictionary<string, string>(),
            docket.DateVariables,
            baseTime
        );

        console.MarkupLine("[bold]Generated URL:[/]");
        console.MarkupLine($"  {resolved.EscapeMarkup()}");
    }

    private static List<DateVariableConfiguration> MergeDateVariables(
        List<DateVariableConfiguration>? baseVars,
        List<DateVariableConfiguration>? overrideVars)
    {
        var result = new List<DateVariableConfiguration>(baseVars ?? new List<DateVariableConfiguration>());

        if (overrideVars == null) return result;

        foreach (var overrideVar in overrideVars)
        {
            var existing = result.FirstOrDefault(v => v.Name == overrideVar.Name);
            if (existing != null)
            {
                result.Remove(existing);
            }
            result.Add(overrideVar);
        }

        return result;
    }

    private static Dictionary<string, string> MergeConfiguration(
        Dictionary<string, string>? baseConfig,
        Dictionary<string, string>? overrideConfig)
    {
        var result = new Dictionary<string, string>(baseConfig ?? new Dictionary<string, string>());

        if (overrideConfig == null) return result;

        foreach (var kvp in overrideConfig)
        {
            result[kvp.Key] = kvp.Value;
        }

        return result;
    }
}
