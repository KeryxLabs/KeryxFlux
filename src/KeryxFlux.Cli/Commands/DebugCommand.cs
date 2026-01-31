using Cocona;
using KeryxFlux.Domain.Models;
using KeryxFlux.Domain.Models.Dockets;
using KeryxFlux.Domain.Utilities;
using Spectre.Console;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace KeryxFlux.Cli.Commands;

public static class DebugCommand
{
    public static async Task Execute(
        [Argument(Description = "Path to docket file")] string path,
        [Option("resolve", Description = "Show variable resolution details")] bool resolve = false,
        [Option("expansion", Description = "Show multi-tenant expansion matrix")] bool expansion = false,
        [Option("at", Description = "Debug at specific time (ISO 8601)")] string? atTime = null,
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

        console.MarkupLine($"[bold cyan]Debug Report for '{docket.Name}'[/]");
        console.MarkupLine($"[dim]Generated at {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC[/]");
        console.WriteLine();

        ShowBasicInfo(console, docket);
        console.WriteLine();

        if (resolve || (!expansion && !resolve))
        {
            ShowVariableResolution(console, docket, baseTime);
            console.WriteLine();
        }

        if (expansion || (!expansion && !resolve))
        {
            await ShowMultiTenantExpansion(console, docket, baseTime);
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

    private static void ShowBasicInfo(IAnsiConsole console, Docket docket)
    {
        var grid = new Grid()
            .AddColumn()
            .AddColumn();

        grid.AddRow("[cyan]Name:[/]", docket.Name ?? "N/A");
        grid.AddRow("[cyan]Version:[/]", docket.Version ?? "N/A");
        grid.AddRow("[cyan]Type:[/]", docket.Type.ToString());
        grid.AddRow("[cyan]Plugin:[/]", docket.PluginLocation ?? "N/A");

        if (docket.Scheduler != null)
        {
            grid.AddRow("[cyan]Schedule:[/]", docket.Scheduler.CronExpression ?? "N/A");
            grid.AddRow("[cyan]Queue:[/]", docket.Scheduler.Queue ?? "default");
        }

        if (docket.Tenants != null && docket.Tenants.Any())
        {
            grid.AddRow("[cyan]Tenants:[/]", $"{docket.Tenants.Count} configured");
        }

        if (docket.Endpoints != null && docket.Endpoints.Any())
        {
            grid.AddRow("[cyan]Endpoints:[/]", $"{docket.Endpoints.Count} configured");
            grid.AddRow("[cyan]Multi-Tenant:[/]", docket.IsMultiTenant ? "[green]Yes[/]" : "[dim]No[/]");
        }

        var panel = new Panel(grid)
            .Header("[bold]Basic Information[/]")
            .BorderColor(Color.Blue);

        console.Write(panel);
    }

    private static void ShowVariableResolution(IAnsiConsole console, Docket docket, DateTimeOffset baseTime)
    {
        var tree = new Tree("[bold]Variable Resolution[/]");

        if (docket.Configuration != null && docket.Configuration.Any())
        {
            var staticNode = tree.AddNode("[yellow]Static Variables[/]");
            foreach (var kvp in docket.Configuration)
            {
                staticNode.AddNode($"[cyan]{{{kvp.Key}}}[/] = [green]{kvp.Value}[/]");
            }
        }

        if (docket.DateVariables != null && docket.DateVariables.Any())
        {
            var dateNode = tree.AddNode($"[yellow]Date Variables[/] [dim](at {baseTime:yyyy-MM-dd HH:mm:ss} UTC)[/]");
            foreach (var dateVar in docket.DateVariables)
            {
                var resolved = DateTemplateResolver.ResolveDateVariable(dateVar, baseTime);
                var calculation = CalculateOffset(dateVar.OffsetExpression, baseTime);
                
                var varNode = dateNode.AddNode($"[cyan]{{{dateVar.Name}}}[/]");
                varNode.AddNode($"Offset: [dim]{dateVar.OffsetExpression}[/]");
                varNode.AddNode($"Format: [dim]{dateVar.Format ?? "default"}[/]");
                varNode.AddNode($"Calculated: {calculation}");
                varNode.AddNode($"Result: [green]{resolved}[/]");
            }
        }

        if (docket.Scheduler?.Server?.Address != null)
        {
            var urlNode = tree.AddNode("[yellow]URL Template[/]");
            urlNode.AddNode($"[dim]{docket.Scheduler.Server.Address.EscapeMarkup()}[/]");

            // Convert double curly brackets {{var}} to single {var} for resolver
            var normalizedTemplate = docket.Scheduler.Server.Address
                .Replace("{{", "{")
                .Replace("}}", "}");

            var resolved = PathTemplateResolver.ResolveWithDates(
                normalizedTemplate,
                docket.Configuration ?? new Dictionary<string, string>(),
                docket.DateVariables,
                baseTime
            );
            urlNode.AddNode($"Resolved: [green]{resolved.EscapeMarkup()}[/]");
        }

        console.Write(tree);
    }

    private static async Task ShowMultiTenantExpansion(IAnsiConsole console, Docket docket, DateTimeOffset baseTime)
    {
        if (docket.Tenants == null || !docket.Tenants.Any())
        {
            console.MarkupLine("[dim]No multi-tenant configuration[/]");
            return;
        }

        console.MarkupLine("[bold]Multi-Tenant Expansion Matrix[/]");
        console.WriteLine();

        var totalJobs = docket.Tenants.Count * (docket.Endpoints?.Count(e => e.Enabled) ?? 0);
        console.MarkupLine($"[cyan]Total Jobs:[/] {totalJobs} (Tenants × Endpoints)");
        console.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Tenant")
            .AddColumn("Endpoint")
            .AddColumn("Date Override")
            .AddColumn("URL Preview");

        foreach (var tenant in docket.Tenants)
        {
            var tenantDateVars = MergeDateVariables(docket.DateVariables, tenant.DateVariables);
            var hasOverride = tenant.DateVariables != null && tenant.DateVariables.Any();

            if (docket.Endpoints == null || !docket.Endpoints.Any())
            {
                table.AddRow(
                    tenant.TenantId ?? "N/A",
                    "[dim]No endpoints[/]",
                    hasOverride ? "[yellow]Yes[/]" : "[dim]No[/]",
                    "[dim]N/A[/]"
                );
                continue;
            }

            foreach (var endpoint in docket.Endpoints.Where(e => e.Enabled))
            {
                var config = MergeConfiguration(
                    docket.BaseConfiguration ?? docket.Configuration,
                    tenant.Configuration,
                    endpoint.Configuration
                );


                var url = config.TryGetValue("base_url", out var baseUrl)
                    ? baseUrl
                    : docket.Scheduler?.Server?.Address ?? "N/A";

                // Convert double curly brackets {{var}} to single {var} for resolver
                var normalizedUrl = url.Replace("{{", "{").Replace("}}", "}");
                var resolved = PathTemplateResolver.ResolveWithDates(normalizedUrl, config, tenantDateVars, baseTime);
                var preview = resolved.Length > 60 ? resolved.Substring(0, 57) + "..." : resolved;

                table.AddRow(
                    tenant.TenantId ?? "N/A",
                    endpoint.EndpointId ?? "N/A",
                    hasOverride ? "[yellow]Yes[/]" : "[dim]No[/]",
                    $"[dim]{preview.EscapeMarkup()}[/]"
                );
            }
        }

        console.Write(table);

        if (docket.Tenants.Any(t => t.DateVariables != null && t.DateVariables.Any()))
        {
            console.WriteLine();
            console.MarkupLine("[yellow]Note:[/] Some tenants have date variable overrides (see 'Date Override' column)");
        }
    }

    private static string CalculateOffset(string? offsetExpression, DateTimeOffset baseTime)
    {
        if (string.IsNullOrWhiteSpace(offsetExpression) || offsetExpression == "0")
            return baseTime.ToString("yyyy-MM-dd HH:mm:ss");

        try
        {
            var offset = ParseOffsetExpression(offsetExpression);
            var result = baseTime.Add(offset);
            return result.ToString("yyyy-MM-dd HH:mm:ss");
        }
        catch
        {
            return "Invalid offset";
        }
    }

    private static TimeSpan ParseOffsetExpression(string expression)
    {
        var sign = expression.StartsWith("-") ? -1 : 1;
        var value = expression.TrimStart('+', '-');

        if (value.EndsWith("s"))
            return TimeSpan.FromSeconds(sign * int.Parse(value.TrimEnd('s')));
        if (value.EndsWith("m"))
            return TimeSpan.FromMinutes(sign * int.Parse(value.TrimEnd('m')));
        if (value.EndsWith("h"))
            return TimeSpan.FromHours(sign * int.Parse(value.TrimEnd('h')));
        if (value.EndsWith("d"))
            return TimeSpan.FromDays(sign * int.Parse(value.TrimEnd('d')));
        if (value.EndsWith("M"))
            return TimeSpan.FromDays(sign * int.Parse(value.TrimEnd('M')) * 30);
        if (value.EndsWith("y"))
            return TimeSpan.FromDays(sign * int.Parse(value.TrimEnd('y')) * 365);

        throw new ArgumentException($"Invalid offset expression: {expression}");
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
        Dictionary<string, string>? tenantConfig,
        Dictionary<string, string>? endpointConfig)
    {
        var result = new Dictionary<string, string>(baseConfig ?? new Dictionary<string, string>());

        if (tenantConfig != null)
        {
            foreach (var kvp in tenantConfig)
            {
                result[kvp.Key] = kvp.Value;
            }
        }

        if (endpointConfig != null)
        {
            foreach (var kvp in endpointConfig)
            {
                result[kvp.Key] = kvp.Value;
            }
        }

        return result;
    }
}
