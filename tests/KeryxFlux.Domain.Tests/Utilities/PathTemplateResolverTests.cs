using KeryxFlux.Domain.Models.Dockets;
using KeryxFlux.Domain.Utilities;
using Shouldly;

namespace KeryxFlux.Domain.Tests.Utilities;

public class PathTemplateResolverTests
{
    [Fact]
    public void Resolve_WithStaticVariables_ResolvesCorrectly()
    {
       
        var template = "/api/{environment}/runs/{org_id}";
        var variables = new Dictionary<string, string>
        {
            { "environment", "production" },
            { "org_id", "ORG001" }
        };

       
        var result = PathTemplateResolver.Resolve(template, variables);

       
        result.ShouldBe("/api/production/runs/ORG001");
    }

    [Fact]
    public void ResolveWithDates_CombinesStaticAndDateVariables()
    {
       
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

       
        var result = PathTemplateResolver.ResolveWithDates(template, staticVars, dateVars, baseTime);

       
        result.ShouldBe("https://ci.example.com/prod/runs?org=ORG001&updated_after=2025-01-15T14:00:00Z");
    }

    [Fact]
    public void ResolveWithDates_OnlyDateVariables_ResolvesCorrectly()
    {
       
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

       
        var result = PathTemplateResolver.ResolveWithDates(template, null, dateVars, baseTime);

       
        result.ShouldBe("https://api.com/data?start=2025-01-14&end=2025-01-15");
    }

    [Fact]
    public void ResolveWithDates_OnlyStaticVariables_ResolvesCorrectly()
    {
       
        var template = "/api/{version}/resource/{id}";
        var staticVars = new Dictionary<string, string>
        {
            { "version", "v2" },
            { "id", "12345" }
        };

       
        var result = PathTemplateResolver.ResolveWithDates(template, staticVars, null);

       
        result.ShouldBe("/api/v2/resource/12345");
    }

    [Fact]
    public void ResolveWithDates_NoVariables_ReturnsOriginalTemplate()
    {
       
        var template = "https://api.com/static/path";

       
        var result = PathTemplateResolver.ResolveWithDates(template, null, null);

       
        result.ShouldBe(template);
    }

    [Fact]
    public void HasUnresolvedVariables_WithUnresolvedVars_ReturnsTrue()
    {
       
        var path = "/api/{environment}/data";

       
        var hasUnresolved = PathTemplateResolver.HasUnresolvedVariables(path);

       
        hasUnresolved.ShouldBeTrue();
    }

    [Fact]
    public void HasUnresolvedVariables_WithNoVars_ReturnsFalse()
    {
       
        var path = "/api/production/data";

       
        var hasUnresolved = PathTemplateResolver.HasUnresolvedVariables(path);

       
        hasUnresolved.ShouldBeFalse();
    }

    [Fact]
    public void ExtractVariableNames_ExtractsAllVariables()
    {
       
        var template = "/api/{environment}/{version}/resource/{id}?filter={status}";

       
        var variables = PathTemplateResolver.ExtractVariableNames(template).ToList();

       
        variables.Count.ShouldBe(4);
        variables.ShouldContain("environment");
        variables.ShouldContain("version");
        variables.ShouldContain("id");
        variables.ShouldContain("status");
    }

    [Fact]
    public void ValidateVariables_AllPresent_ReturnsTrue()
    {
       
        var template = "/api/{env}/{id}";
        var variables = new Dictionary<string, string>
        {
            { "env", "prod" },
            { "id", "123" }
        };

       
        var isValid = PathTemplateResolver.ValidateVariables(template, variables, out var missing);

       
        isValid.ShouldBeTrue();
        missing.ShouldBeEmpty();
    }

    [Fact]
    public void ValidateVariables_MissingVariables_ReturnsFalse()
    {
       
        var template = "/api/{env}/{id}/{region}";
        var variables = new Dictionary<string, string>
        {
            { "env", "prod" }
        };

       
        var isValid = PathTemplateResolver.ValidateVariables(template, variables, out var missing);

       
        isValid.ShouldBeFalse();
        missing.Count.ShouldBe(2);
        missing.ShouldContain("id");
        missing.ShouldContain("region");
    }
}
