using KeryxFlux.Domain.Models.Dockets;
using KeryxFlux.Domain.Utilities;
using Xunit;

namespace KeryxFlux.Domain.Tests.Utilities;

public class PathTemplateResolverTests
{
    [Fact]
    public void Resolve_WithStaticVariables_ResolvesCorrectly()
    {
        // Arrange
        var template = "/api/{environment}/runs/{org_id}";
        var variables = new Dictionary<string, string>
        {
            { "environment", "production" },
            { "org_id", "ORG001" }
        };

        // Act
        var result = PathTemplateResolver.Resolve(template, variables);

        // Assert
        Assert.Equal("/api/production/runs/ORG001", result);
    }

    [Fact]
    public void ResolveWithDates_CombinesStaticAndDateVariables()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2025, 1, 15, 15, 0, 0, TimeSpan.Zero);
        var template = "https://ci.example.com/{environment}/runs?org={org_id}&updated_after={lookback_date}";
        
        var staticVars = new Dictionary<string, string>
        {
            { "environment", "prod" },
            { "org_id", "ORG001" }
        };

        var dateVars = new List<DateVariableConfiguration>
        {
            new()
            {
                Name = "lookback_date",
                OffsetExpression = "-1h",
                Format = "yyyy-MM-dd'T'HH:mm:ss'Z'",
                Timezone = "UTC"
            }
        };

        // Act
        var result = PathTemplateResolver.ResolveWithDates(template, staticVars, dateVars, baseTime);

        // Assert
        Assert.Equal("https://ci.example.com/prod/runs?org=ORG001&updated_after=2025-01-15T14:00:00Z", result);
    }

    [Fact]
    public void ResolveWithDates_OnlyDateVariables_ResolvesCorrectly()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero);
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
        var result = PathTemplateResolver.ResolveWithDates(template, null, dateVars, baseTime);

        // Assert
        Assert.Equal("https://api.com/data?start=2025-01-14&end=2025-01-15", result);
    }

    [Fact]
    public void ResolveWithDates_OnlyStaticVariables_ResolvesCorrectly()
    {
        // Arrange
        var template = "/api/{version}/resource/{id}";
        var staticVars = new Dictionary<string, string>
        {
            { "version", "v2" },
            { "id", "12345" }
        };

        // Act
        var result = PathTemplateResolver.ResolveWithDates(template, staticVars, null);

        // Assert
        Assert.Equal("/api/v2/resource/12345", result);
    }

    [Fact]
    public void ResolveWithDates_NoVariables_ReturnsOriginalTemplate()
    {
        // Arrange
        var template = "https://api.com/static/path";

        // Act
        var result = PathTemplateResolver.ResolveWithDates(template, null, null);

        // Assert
        Assert.Equal(template, result);
    }

    [Fact]
    public void HasUnresolvedVariables_WithUnresolvedVars_ReturnsTrue()
    {
        // Arrange
        var path = "/api/{environment}/data";

        // Act
        var hasUnresolved = PathTemplateResolver.HasUnresolvedVariables(path);

        // Assert
        Assert.True(hasUnresolved);
    }

    [Fact]
    public void HasUnresolvedVariables_WithNoVars_ReturnsFalse()
    {
        // Arrange
        var path = "/api/production/data";

        // Act
        var hasUnresolved = PathTemplateResolver.HasUnresolvedVariables(path);

        // Assert
        Assert.False(hasUnresolved);
    }

    [Fact]
    public void ExtractVariableNames_ExtractsAllVariables()
    {
        // Arrange
        var template = "/api/{environment}/{version}/resource/{id}?filter={status}";

        // Act
        var variables = PathTemplateResolver.ExtractVariableNames(template).ToList();

        // Assert
        Assert.Equal(4, variables.Count);
        Assert.Contains("environment", variables);
        Assert.Contains("version", variables);
        Assert.Contains("id", variables);
        Assert.Contains("status", variables);
    }

    [Fact]
    public void ValidateVariables_AllPresent_ReturnsTrue()
    {
        // Arrange
        var template = "/api/{env}/{id}";
        var variables = new Dictionary<string, string>
        {
            { "env", "prod" },
            { "id", "123" }
        };

        // Act
        var isValid = PathTemplateResolver.ValidateVariables(template, variables, out var missing);

        // Assert
        Assert.True(isValid);
        Assert.Empty(missing);
    }

    [Fact]
    public void ValidateVariables_MissingVariables_ReturnsFalse()
    {
        // Arrange
        var template = "/api/{env}/{id}/{region}";
        var variables = new Dictionary<string, string>
        {
            { "env", "prod" }
        };

        // Act
        var isValid = PathTemplateResolver.ValidateVariables(template, variables, out var missing);

        // Assert
        Assert.False(isValid);
        Assert.Equal(2, missing.Count);
        Assert.Contains("id", missing);
        Assert.Contains("region", missing);
    }
}
