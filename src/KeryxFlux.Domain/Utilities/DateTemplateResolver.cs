using KeryxFlux.Domain.Models.Dockets;
using System.Globalization;
using System.Text.RegularExpressions;

namespace KeryxFlux.Domain.Utilities;

/// <summary>
/// Resolves date/time variables in templates based on DateVariableConfiguration.
/// Supports time offsets and custom formatting for dynamic URL generation.
/// </summary>
public static class DateTemplateResolver
{
    private static readonly Regex OffsetRegex = new(@"^([+-]?)(\d+)([yMdhms])$", RegexOptions.Compiled);

    /// <summary>
    /// Resolve date variables in a template using configured date variable definitions
    /// </summary>
    /// <param name="template">URL template containing date variables like {startDate}, {endDate}</param>
    /// <param name="dateVariables">List of date variable configurations</param>
    /// <param name="baseTime">Base time to calculate offsets from (default: UtcNow)</param>
    /// <returns>Template with date variables resolved</returns>
    public static string Resolve(
        string template, 
        IReadOnlyList<DateVariableConfiguration>? dateVariables,
        DateTimeOffset? baseTime = null)
    {
        if (string.IsNullOrWhiteSpace(template) || dateVariables == null || dateVariables.Count == 0)
        {
            return template;
        }

        var effectiveBaseTime = baseTime ?? DateTimeOffset.UtcNow;
        var result = template;

        foreach (var dateVar in dateVariables)
        {
            var resolvedValue = ResolveDateVariable(dateVar, effectiveBaseTime);
            result = result.Replace($"{{{dateVar.Name}}}", resolvedValue, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    /// <summary>
    /// Resolve a single date variable to its string value
    /// </summary>
    /// <param name="dateVar">Date variable configuration</param>
    /// <param name="baseTime">Base time to calculate offset from</param>
    /// <returns>Formatted date/time string</returns>
    public static string ResolveDateVariable(DateVariableConfiguration dateVar, DateTimeOffset baseTime)
    {
        // Parse offset expression
        var offset = ParseOffsetExpression(dateVar.OffsetExpression);
        
        // Apply offset to base time
        var calculatedTime = baseTime.Add(offset);

        // Convert to specified timezone
        var timeZoneInfo = GetTimeZoneInfo(dateVar.Timezone);
        var zonedTime = TimeZoneInfo.ConvertTime(calculatedTime, timeZoneInfo);

        // Format the result
        return FormatDateTime(zonedTime, dateVar.Format);
    }

    /// <summary>
    /// Parse an offset expression into a TimeSpan
    /// Examples: "-1h" = -1 hour, "-30d" = -30 days, "+2m" = +2 minutes
    /// </summary>
    private static TimeSpan ParseOffsetExpression(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression) || expression == "0")
        {
            return TimeSpan.Zero;
        }

        var match = OffsetRegex.Match(expression.Trim());
        if (!match.Success)
        {
            throw new ArgumentException(
                $"Invalid offset expression: '{expression}'. " +
                $"Expected format: [+/-][number][unit] where unit is y/M/d/h/m/s. " +
                $"Examples: '-1h', '-30d', '+15m'");
        }

        var sign = match.Groups[1].Value == "-" ? -1 : 1;
        var value = int.Parse(match.Groups[2].Value);
        var unit = match.Groups[3].Value;

        return unit switch
        {
            "s" => TimeSpan.FromSeconds(sign * value),
            "m" => TimeSpan.FromMinutes(sign * value),
            "h" => TimeSpan.FromHours(sign * value),
            "d" => TimeSpan.FromDays(sign * value),
            "M" => TimeSpan.FromDays(sign * value * 30), // Approximate month as 30 days
            "y" => TimeSpan.FromDays(sign * value * 365), // Approximate year as 365 days
            _ => throw new ArgumentException($"Unknown time unit: '{unit}'. Valid units: y, M, d, h, m, s")
        };
    }

    /// <summary>
    /// Get TimeZoneInfo from IANA time zone identifier
    /// </summary>
    private static TimeZoneInfo GetTimeZoneInfo(string timezone)
    {
        try
        {
            // Try to get the time zone (supports both IANA and Windows time zone IDs)
            return TimeZoneInfo.FindSystemTimeZoneById(timezone);
        }
        catch (TimeZoneNotFoundException)
        {
            // Fallback to UTC if timezone not found
            return TimeZoneInfo.Utc;
        }
    }

    /// <summary>
    /// Format DateTime according to the specified format string
    /// Supports special formats: "unix", "unix_ms" for Unix timestamps
    /// </summary>
    private static string FormatDateTime(DateTimeOffset dateTime, string format)
    {
        return format.ToLowerInvariant() switch
        {
            "unix" => dateTime.ToUnixTimeSeconds().ToString(),
            "unix_ms" => dateTime.ToUnixTimeMilliseconds().ToString(),
            _ => dateTime.ToString(format, CultureInfo.InvariantCulture)
        };
    }

    /// <summary>
    /// Create date variables for a time range (start and end)
    /// Useful for backfill scenarios: "get all data from 30 days ago to now"
    /// </summary>
    /// <param name="startOffsetExpression">Offset for start date (e.g., "-30d")</param>
    /// <param name="endOffsetExpression">Offset for end date (e.g., "0" for now)</param>
    /// <param name="format">Date format</param>
    /// <param name="startVarName">Variable name for start date (default: "start_date")</param>
    /// <param name="endVarName">Variable name for end date (default: "end_date")</param>
    /// <returns>List of two date variable configurations</returns>
    public static List<DateVariableConfiguration> CreateTimeRangeVariables(
        string startOffsetExpression,
        string endOffsetExpression = "0",
        string format = "yyyy-MM-dd'T'HH:mm:ss'Z'",
        string startVarName = "start_date",
        string endVarName = "end_date")
    {
        return new List<DateVariableConfiguration>
        {
            new()
            {
                Name = startVarName,
                OffsetExpression = startOffsetExpression,
                Format = format,
                Description = $"Start of time range (offset: {startOffsetExpression})"
            },
            new()
            {
                Name = endVarName,
                OffsetExpression = endOffsetExpression,
                Format = format,
                Description = $"End of time range (offset: {endOffsetExpression})"
            }
        };
    }

    /// <summary>
    /// Validate that all date variables have valid offset expressions
    /// </summary>
    public static bool ValidateDateVariables(
        IReadOnlyList<DateVariableConfiguration> dateVariables,
        out List<string> errors)
    {
        errors = new List<string>();

        if (dateVariables == null || dateVariables.Count == 0)
        {
            return true; // No variables to validate
        }

        foreach (var dateVar in dateVariables)
        {
            try
            {
                ParseOffsetExpression(dateVar.OffsetExpression);
            }
            catch (Exception ex)
            {
                errors.Add($"Invalid offset expression for variable '{dateVar.Name}': {ex.Message}");
            }

            // Validate timezone
            try
            {
                GetTimeZoneInfo(dateVar.Timezone);
            }
            catch (Exception ex)
            {
                errors.Add($"Invalid timezone for variable '{dateVar.Name}': {ex.Message}");
            }
        }

        return errors.Count == 0;
    }
}
