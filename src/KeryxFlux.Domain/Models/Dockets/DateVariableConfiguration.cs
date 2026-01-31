using YamlDotNet.Serialization;

namespace KeryxFlux.Domain.Models.Dockets;

/// <summary>
/// Defines a date/time variable that can be used in URL templates.
/// Supports offsets from current time and custom formatting.
/// 
/// Example YAML:
/// date_variables:
///   - name: lookback_date
///     offset_expression: "-1h"
///     format: "yyyy-MM-dd'T'HH:mm:ss'Z'"
///     
///   - name: backfill_start
///     offset_expression: "-30d"
///     format: "yyyy-MM-dd"
/// </summary>
public sealed class DateVariableConfiguration
{
    /// <summary>
    /// Variable name to use in templates (e.g., {lookback_date})
    /// </summary>
    [YamlMember(Alias = "name")]
    public required string Name { get; init; }

    /// <summary>
    /// Time offset expression from current time.
    /// Format: [+/-][number][unit]
    /// Units: y (years), M (months), d (days), h (hours), m (minutes), s (seconds)
    /// 
    /// Examples:
    /// - "-1h"      = 1 hour ago
    /// - "-30d"     = 30 days ago
    /// - "+2h"      = 2 hours from now
    /// - "-15m"     = 15 minutes ago
    /// - "0"        = current time (default)
    /// </summary>
    [YamlMember(Alias = "offset_expression")]
    public string OffsetExpression { get; init; } = "0";

    /// <summary>
    /// Date/time format string (C# DateTime format).
    /// 
    /// Common formats:
    /// - "yyyy-MM-dd"                        = 2025-01-15
    /// - "yyyy-MM-dd'T'HH:mm:ss'Z'"          = 2025-01-15T14:30:00Z (ISO 8601)
    /// - "yyyy-MM-dd'T'HH:mm:ss.fff'Z'"      = 2025-01-15T14:30:00.123Z
    /// - "MM/dd/yyyy"                        = 01/15/2025
    /// - "yyyyMMdd"                          = 20250115
    /// - "yyyy-MM-dd HH:mm:ss"               = 2025-01-15 14:30:00
    /// - Unix timestamp (seconds): "unix"
    /// - Unix timestamp (milliseconds): "unix_ms"
    /// 
    /// Default: ISO 8601 format (yyyy-MM-dd'T'HH:mm:ss'Z')
    /// </summary>
    [YamlMember(Alias = "format")]
    public string Format { get; init; } = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    /// <summary>
    /// Time zone for the date calculation (IANA time zone database format).
    /// Examples: "UTC", "America/New_York", "Europe/London"
    /// Default: "UTC"
    /// </summary>
    [YamlMember(Alias = "timezone")]
    public string Timezone { get; init; } = "UTC";

    /// <summary>
    /// Optional description for documentation purposes
    /// </summary>
    [YamlMember(Alias = "description")]
    public string? Description { get; init; }
}
