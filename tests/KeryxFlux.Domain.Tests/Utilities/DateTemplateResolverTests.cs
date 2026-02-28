using KeryxFlux.Domain.Models.Dockets;
using KeryxFlux.Domain.Utilities;
using Shouldly;

namespace KeryxFlux.Domain.Tests.Utilities;

public class DateTemplateResolverTests
{
    [Fact]
    public void ResolveDateVariable_WithHourOffset_ReturnsCorrectDate()
    {
       
        var baseTime = new DateTimeOffset(2025, 1, 15, 15, 30, 0, TimeSpan.Zero);
        var dateVar = new DateVariableConfiguration
        {
            Name = "lookback_date",
            OffsetExpression = "-1h",
            Format = "yyyy-MM-dd'T'HH:mm:ss'Z'",
            Timezone = "UTC"
        };

       
        var result = DateTemplateResolver.ResolveDateVariable(dateVar, baseTime);

       
        result.ShouldBe("2025-01-15T14:30:00Z");
    }

    [Fact]
    public void ResolveDateVariable_WithDayOffset_ReturnsCorrectDate()
    {
       
        var baseTime = new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero);
        var dateVar = new DateVariableConfiguration
        {
            Name = "backfill_start",
            OffsetExpression = "-30d",
            Format = "yyyy-MM-dd",
            Timezone = "UTC"
        };

       
        var result = DateTemplateResolver.ResolveDateVariable(dateVar, baseTime);

       
        result.ShouldBe("2024-12-16");
    }

    [Fact]
    public void ResolveDateVariable_WithMinuteOffset_ReturnsCorrectDate()
    {
       
        var baseTime = new DateTimeOffset(2025, 1, 15, 14, 30, 0, TimeSpan.Zero);
        var dateVar = new DateVariableConfiguration
        {
            Name = "recent",
            OffsetExpression = "-15m",
            Format = "HH:mm:ss",
            Timezone = "UTC"
        };

       
        var result = DateTemplateResolver.ResolveDateVariable(dateVar, baseTime);

       
        result.ShouldBe("14:15:00");
    }

    [Fact]
    public void ResolveDateVariable_WithUnixFormat_ReturnsTimestamp()
    {
       
        var baseTime = new DateTimeOffset(2025, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var dateVar = new DateVariableConfiguration
        {
            Name = "timestamp",
            OffsetExpression = "0",
            Format = "unix",
            Timezone = "UTC"
        };

       
        var result = DateTemplateResolver.ResolveDateVariable(dateVar, baseTime);

       
        var expectedTimestamp = baseTime.ToUnixTimeSeconds().ToString();
        result.ShouldBe(expectedTimestamp);
    }

    [Fact]
    public void ResolveDateVariable_WithUnixMsFormat_ReturnsTimestampInMilliseconds()
    {
       
        var baseTime = new DateTimeOffset(2025, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var dateVar = new DateVariableConfiguration
        {
            Name = "timestamp_ms",
            OffsetExpression = "0",
            Format = "unix_ms",
            Timezone = "UTC"
        };

       
        var result = DateTemplateResolver.ResolveDateVariable(dateVar, baseTime);

       
        var expectedTimestamp = baseTime.ToUnixTimeMilliseconds().ToString();
        result.ShouldBe(expectedTimestamp);
    }

    [Fact]
    public void ResolveDateVariable_WithPositiveOffset_ReturnsFutureDate()
    {
       
        var baseTime = new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero);
        var dateVar = new DateVariableConfiguration
        {
            Name = "future_date",
            OffsetExpression = "+2h",
            Format = "yyyy-MM-dd'T'HH:mm:ss'Z'",
            Timezone = "UTC"
        };

       
        var result = DateTemplateResolver.ResolveDateVariable(dateVar, baseTime);

       
        result.ShouldBe("2025-01-15T12:00:00Z");
    }

    [Fact]
    public void Resolve_WithMultipleDateVariables_ResolvesAll()
    {
       
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

       
        var result = DateTemplateResolver.Resolve(template, dateVars, baseTime);

       
        result.ShouldBe("https://api.com/data?start=2025-01-14&end=2025-01-15");
    }

    [Fact]
    public void Resolve_WithNoDateVariables_ReturnsOriginalTemplate()
    {
       
        var template = "https://api.com/data";
        
       
        var result = DateTemplateResolver.Resolve(template, null);

       
        result.ShouldBe(template);
    }

    [Fact]
    public void Resolve_WithEmptyTemplate_ReturnsEmptyString()
    {
       
        var dateVars = new List<DateVariableConfiguration>
        {
            new()
            {
                Name = "test",
                OffsetExpression = "-1h",
                Format = "yyyy-MM-dd"
            }
        };

       
        var result = DateTemplateResolver.Resolve("", dateVars);

       
        result.ShouldBe("");
    }

    [Theory]
    [InlineData("-1s", 1)]  // 1 second ago
    [InlineData("-5m", 300)]  // 5 minutes ago (300 seconds)
    [InlineData("-2h", 7200)]  // 2 hours ago (7200 seconds)
    [InlineData("-1d", 86400)]  // 1 day ago (86400 seconds)
    public void ResolveDateVariable_WithVariousOffsets_CalculatesCorrectly(string offset, int expectedSecondsDiff)
    {
       
        var baseTime = new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var dateVar = new DateVariableConfiguration
        {
            Name = "test",
            OffsetExpression = offset,
            Format = "unix",
            Timezone = "UTC"
        };

       
        var result = DateTemplateResolver.ResolveDateVariable(dateVar, baseTime);
        var resultTimestamp = long.Parse(result);
        var expectedTimestamp = baseTime.ToUnixTimeSeconds() - expectedSecondsDiff;

       
        resultTimestamp.ShouldBe(expectedTimestamp);
    }

    [Fact]
    public void ValidateDateVariables_WithValidConfigs_ReturnsTrue()
    {
       
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

       
        var isValid = DateTemplateResolver.ValidateDateVariables(dateVars, out var errors);

       
        isValid.ShouldBeTrue();
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void ValidateDateVariables_WithInvalidOffset_ReturnsFalse()
    {
       
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

       
        var isValid = DateTemplateResolver.ValidateDateVariables(dateVars, out var errors);

       
        isValid.ShouldBeFalse();
        errors.ShouldNotBeEmpty();
        errors[0].ShouldContain("invalid");
    }

    [Fact]
    public void CreateTimeRangeVariables_CreatesStartAndEndVariables()
    {
       
        var variables = DateTemplateResolver.CreateTimeRangeVariables(
            startOffsetExpression: "-30d",
            endOffsetExpression: "0",
            format: "yyyy-MM-dd"
        );

       
        variables.Count.ShouldBe(2);
        variables[0].Name.ShouldBe("start_date");
        variables[0].OffsetExpression.ShouldBe("-30d");
        variables[1].Name.ShouldBe("end_date");
        variables[1].OffsetExpression.ShouldBe("0");
    }

    [Fact]
    public void ResolveDateVariable_WithZeroOffset_ReturnsCurrentTime()
    {
       
        var baseTime = new DateTimeOffset(2025, 1, 15, 14, 30, 45, TimeSpan.Zero);
        var dateVar = new DateVariableConfiguration
        {
            Name = "now",
            OffsetExpression = "0",
            Format = "yyyy-MM-dd'T'HH:mm:ss'Z'",
            Timezone = "UTC"
        };

       
        var result = DateTemplateResolver.ResolveDateVariable(dateVar, baseTime);

       
        result.ShouldBe("2025-01-15T14:30:45Z");
    }

    [Fact]
    public void Resolve_CaseInsensitive_ResolvesVariables()
    {
       
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

       
        var result = DateTemplateResolver.Resolve(template, dateVars, baseTime);

       
        result.ShouldBe("https://api.com/data?date=2025-01-15");
    }
}
