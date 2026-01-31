using KeryxFlux.Domain.Models.Dockets;
using KeryxFlux.Domain.Utilities;
using Xunit;

namespace KeryxFlux.Domain.Tests.Utilities;

public class DateTemplateResolverTests
{
    [Fact]
    public void ResolveDateVariable_WithHourOffset_ReturnsCorrectDate()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2025, 1, 15, 15, 30, 0, TimeSpan.Zero);
        var dateVar = new DateVariableConfiguration
        {
            Name = "lookback_date",
            OffsetExpression = "-1h",
            Format = "yyyy-MM-dd'T'HH:mm:ss'Z'",
            Timezone = "UTC"
        };

        // Act
        var result = DateTemplateResolver.ResolveDateVariable(dateVar, baseTime);

        // Assert
        Assert.Equal("2025-01-15T14:30:00Z", result);
    }

    [Fact]
    public void ResolveDateVariable_WithDayOffset_ReturnsCorrectDate()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero);
        var dateVar = new DateVariableConfiguration
        {
            Name = "backfill_start",
            OffsetExpression = "-30d",
            Format = "yyyy-MM-dd",
            Timezone = "UTC"
        };

        // Act
        var result = DateTemplateResolver.ResolveDateVariable(dateVar, baseTime);

        // Assert
        Assert.Equal("2024-12-16", result);
    }

    [Fact]
    public void ResolveDateVariable_WithMinuteOffset_ReturnsCorrectDate()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2025, 1, 15, 14, 30, 0, TimeSpan.Zero);
        var dateVar = new DateVariableConfiguration
        {
            Name = "recent",
            OffsetExpression = "-15m",
            Format = "HH:mm:ss",
            Timezone = "UTC"
        };

        // Act
        var result = DateTemplateResolver.ResolveDateVariable(dateVar, baseTime);

        // Assert
        Assert.Equal("14:15:00", result);
    }

    [Fact]
    public void ResolveDateVariable_WithUnixFormat_ReturnsTimestamp()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2025, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var dateVar = new DateVariableConfiguration
        {
            Name = "timestamp",
            OffsetExpression = "0",
            Format = "unix",
            Timezone = "UTC"
        };

        // Act
        var result = DateTemplateResolver.ResolveDateVariable(dateVar, baseTime);

        // Assert
        var expectedTimestamp = baseTime.ToUnixTimeSeconds().ToString();
        Assert.Equal(expectedTimestamp, result);
    }

    [Fact]
    public void ResolveDateVariable_WithUnixMsFormat_ReturnsTimestampInMilliseconds()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2025, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var dateVar = new DateVariableConfiguration
        {
            Name = "timestamp_ms",
            OffsetExpression = "0",
            Format = "unix_ms",
            Timezone = "UTC"
        };

        // Act
        var result = DateTemplateResolver.ResolveDateVariable(dateVar, baseTime);

        // Assert
        var expectedTimestamp = baseTime.ToUnixTimeMilliseconds().ToString();
        Assert.Equal(expectedTimestamp, result);
    }

    [Fact]
    public void ResolveDateVariable_WithPositiveOffset_ReturnsFutureDate()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero);
        var dateVar = new DateVariableConfiguration
        {
            Name = "future_date",
            OffsetExpression = "+2h",
            Format = "yyyy-MM-dd'T'HH:mm:ss'Z'",
            Timezone = "UTC"
        };

        // Act
        var result = DateTemplateResolver.ResolveDateVariable(dateVar, baseTime);

        // Assert
        Assert.Equal("2025-01-15T12:00:00Z", result);
    }

    [Fact]
    public void Resolve_WithMultipleDateVariables_ResolvesAll()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2025, 1, 15, 15, 0, 0, TimeSpan.Zero);
        var template = "https://api.com/data?start={start_date}&end={end_date}";
        var dateVars = new List<DateVariableConfiguration>
        {
            new()
            {
                Name = "start_date",
                OffsetExpression = "-1d",
                Format = "yyyy-MM-dd",
                Timezone = "UTC"
            },
            new()
            {
                Name = "end_date",
                OffsetExpression = "0",
                Format = "yyyy-MM-dd",
                Timezone = "UTC"
            }
        };

        // Act
        var result = DateTemplateResolver.Resolve(template, dateVars, baseTime);

        // Assert
        Assert.Equal("https://api.com/data?start=2025-01-14&end=2025-01-15", result);
    }

    [Fact]
    public void Resolve_WithNoDateVariables_ReturnsOriginalTemplate()
    {
        // Arrange
        var template = "https://api.com/data";
        
        // Act
        var result = DateTemplateResolver.Resolve(template, null);

        // Assert
        Assert.Equal(template, result);
    }

    [Fact]
    public void Resolve_WithEmptyTemplate_ReturnsEmptyString()
    {
        // Arrange
        var dateVars = new List<DateVariableConfiguration>
        {
            new()
            {
                Name = "test",
                OffsetExpression = "-1h",
                Format = "yyyy-MM-dd"
            }
        };

        // Act
        var result = DateTemplateResolver.Resolve("", dateVars);

        // Assert
        Assert.Equal("", result);
    }

    [Theory]
    [InlineData("-1s", 1)]  // 1 second ago
    [InlineData("-5m", 300)]  // 5 minutes ago (300 seconds)
    [InlineData("-2h", 7200)]  // 2 hours ago (7200 seconds)
    [InlineData("-1d", 86400)]  // 1 day ago (86400 seconds)
    public void ResolveDateVariable_WithVariousOffsets_CalculatesCorrectly(string offset, int expectedSecondsDiff)
    {
        // Arrange
        var baseTime = new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var dateVar = new DateVariableConfiguration
        {
            Name = "test",
            OffsetExpression = offset,
            Format = "unix",
            Timezone = "UTC"
        };

        // Act
        var result = DateTemplateResolver.ResolveDateVariable(dateVar, baseTime);
        var resultTimestamp = long.Parse(result);
        var expectedTimestamp = baseTime.ToUnixTimeSeconds() - expectedSecondsDiff;

        // Assert
        Assert.Equal(expectedTimestamp, resultTimestamp);
    }

    [Fact]
    public void ValidateDateVariables_WithValidConfigs_ReturnsTrue()
    {
        // Arrange
        var dateVars = new List<DateVariableConfiguration>
        {
            new()
            {
                Name = "valid1",
                OffsetExpression = "-1h",
                Format = "yyyy-MM-dd",
                Timezone = "UTC"
            },
            new()
            {
                Name = "valid2",
                OffsetExpression = "-30d",
                Format = "unix",
                Timezone = "UTC"
            }
        };

        // Act
        var isValid = DateTemplateResolver.ValidateDateVariables(dateVars, out var errors);

        // Assert
        Assert.True(isValid);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateDateVariables_WithInvalidOffset_ReturnsFalse()
    {
        // Arrange
        var dateVars = new List<DateVariableConfiguration>
        {
            new()
            {
                Name = "invalid",
                OffsetExpression = "bad-offset",
                Format = "yyyy-MM-dd",
                Timezone = "UTC"
            }
        };

        // Act
        var isValid = DateTemplateResolver.ValidateDateVariables(dateVars, out var errors);

        // Assert
        Assert.False(isValid);
        Assert.NotEmpty(errors);
        Assert.Contains("invalid", errors[0]);
    }

    [Fact]
    public void CreateTimeRangeVariables_CreatesStartAndEndVariables()
    {
        // Act
        var variables = DateTemplateResolver.CreateTimeRangeVariables(
            startOffsetExpression: "-30d",
            endOffsetExpression: "0",
            format: "yyyy-MM-dd"
        );

        // Assert
        Assert.Equal(2, variables.Count);
        Assert.Equal("start_date", variables[0].Name);
        Assert.Equal("-30d", variables[0].OffsetExpression);
        Assert.Equal("end_date", variables[1].Name);
        Assert.Equal("0", variables[1].OffsetExpression);
    }

    [Fact]
    public void ResolveDateVariable_WithZeroOffset_ReturnsCurrentTime()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2025, 1, 15, 14, 30, 45, TimeSpan.Zero);
        var dateVar = new DateVariableConfiguration
        {
            Name = "now",
            OffsetExpression = "0",
            Format = "yyyy-MM-dd'T'HH:mm:ss'Z'",
            Timezone = "UTC"
        };

        // Act
        var result = DateTemplateResolver.ResolveDateVariable(dateVar, baseTime);

        // Assert
        Assert.Equal("2025-01-15T14:30:45Z", result);
    }

    [Fact]
    public void Resolve_CaseInsensitive_ResolvesVariables()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero);
        var template = "https://api.com/data?date={LookBack_Date}";  // Mixed case
        var dateVars = new List<DateVariableConfiguration>
        {
            new()
            {
                Name = "lookback_date",  // Lower case
                OffsetExpression = "-1h",
                Format = "yyyy-MM-dd",
                Timezone = "UTC"
            }
        };

        // Act
        var result = DateTemplateResolver.Resolve(template, dateVars, baseTime);

        // Assert
        Assert.Equal("https://api.com/data?date=2025-01-15", result);
    }
}
