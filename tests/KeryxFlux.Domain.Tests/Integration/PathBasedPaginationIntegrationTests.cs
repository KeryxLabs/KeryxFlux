using KeryxFlux.Domain.Models.Dockets;
using KeryxFlux.Domain.Utilities;
using Shouldly;
using Xunit;

namespace KeryxFlux.Domain.Tests.Integration;

public class PathBasedPaginationIntegrationTests
{
    [Fact]
    public void SimplePathPagination_ResolvesCorrectly()
    {
        var baseTime = new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero);
        var template = "https://api.com/patients/{page}/{page_size}";
        var staticVars = new Dictionary<string, string>
        {
            { "page", "1" },
            { "page_size", "50" }
        };

        var result = PathTemplateResolver.ResolveWithDates(template, staticVars, null, baseTime);

        result.ShouldBe("https://api.com/patients/1/50");
    }

    [Fact]
    public void PathPagination_CombinesWithDateVariables()
    {
        var baseTime = new DateTimeOffset(2025, 1, 15, 15, 0, 0, TimeSpan.Zero);
        var template = "https://api.com/patients/{page}/{page_size}?since={lookback_date}";
        var staticVars = new Dictionary<string, string>
        {
            { "page", "2" },
            { "page_size", "100" }
        };
        var dateVars = new List<DateVariableConfiguration>
        {
            new() { Name = "lookback_date", OffsetExpression = "-1h", Format = "yyyy-MM-dd'T'HH:mm:ss'Z'" }
        };

        var result = PathTemplateResolver.ResolveWithDates(template, staticVars, dateVars, baseTime);

        result.ShouldBe("https://api.com/patients/2/100?since=2025-01-15T14:00:00Z");
    }

    [Fact]
    public void UsersExactScenario_WorksAsExpected()
    {
        var baseTime = new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero);
        var template = "https://api.com/Patients/GetLatest/{patient_id}/{page_number}/{page_size}?since={lookback_date}";
        var staticVars = new Dictionary<string, string>
        {
            { "patient_id", "12345" },
            { "page_number", "3" },
            { "page_size", "50" }
        };
        var dateVars = new List<DateVariableConfiguration>
        {
            new() { Name = "lookback_date", OffsetExpression = "-24h", Format = "yyyy-MM-dd" }
        };

        var result = PathTemplateResolver.ResolveWithDates(template, staticVars, dateVars, baseTime);

        result.ShouldBe("https://api.com/Patients/GetLatest/12345/3/50?since=2025-01-14");
    }

    [Fact]
    public void OffsetLimitInPath_ResolvesCorrectly()
    {
        var baseTime = new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var template = "https://api.com/data/{offset}/{limit}?updated_after={since_date}";
        var staticVars = new Dictionary<string, string>
        {
            { "offset", "200" },
            { "limit", "100" }
        };
        var dateVars = new List<DateVariableConfiguration>
        {
            new() { Name = "since_date", OffsetExpression = "-2h", Format = "yyyy-MM-dd'T'HH:mm:ss'Z'" }
        };

        var result = PathTemplateResolver.ResolveWithDates(template, staticVars, dateVars, baseTime);

        result.ShouldBe("https://api.com/data/200/100?updated_after=2025-01-15T10:00:00Z");
    }

    [Fact]
    public void PathPagination_MultiTenantWithPath_ResolvesCorrectly()
    {
        // Arrange - /{tenant}/resource/{page}/{size}
        var baseTime = new DateTimeOffset(2025, 1, 15, 15, 0, 0, TimeSpan.Zero);
        
        var template = "{api_base}/{tenant_id}/resource/{resource_type}/{page}/{page_size}?since={lookback_date}";
        
        var staticVars = new Dictionary<string, string>
        {
            { "api_base", "https://api.ehr.com" },
            { "tenant_id", "HOSP001" },
            { "resource_type", "Patient" },
            { "page", "1" },
            { "page_size", "100" }
        };

        var dateVars = new List<DateVariableConfiguration>
        {
            new()
            {
                Name = "lookback_date",
                OffsetExpression = "-1h",
                Format = "yyyy-MM-dd'T'HH:mm:ss'Z'"
            }
        };

        // Act
        var result = PathTemplateResolver.ResolveWithDates(template, staticVars, dateVars, baseTime);

        result.ShouldBe("https://api.ehr.com/HOSP001/resource/Patient/1/100?since=2025-01-15T14:00:00Z");
    }

    [Fact]
    public void ComplexPath_ResolvesMultipleFilters()
    {
        var baseTime = new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero);
        var template = "https://api.com/{tenant_id}/data/{category}/{page}/{size}?from={start_date}&to={end_date}&status={status}";
        var staticVars = new Dictionary<string, string>
        {
            { "tenant_id", "MAIN_HOSPITAL" },
            { "category", "labs" },
            { "page", "5" },
            { "size", "50" },
            { "status", "final" }
        };
        var dateVars = new List<DateVariableConfiguration>
        {
            new() { Name = "start_date", OffsetExpression = "-7d", Format = "yyyy-MM-dd" },
            new() { Name = "end_date", OffsetExpression = "0", Format = "yyyy-MM-dd" }
        };

        var result = PathTemplateResolver.ResolveWithDates(template, staticVars, dateVars, baseTime);

        result.ShouldBe("https://api.com/MAIN_HOSPITAL/data/labs/5/50?from=2025-01-08&to=2025-01-15&status=final");
    }

    [Fact]
    public void PageProgression_SimulatesMultiplePages()
    {
        var baseTime = new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero);
        var template = "https://api.com/data/{page}/100?since={lookback_date}";
        var dateVars = new List<DateVariableConfiguration>
        {
            new() { Name = "lookback_date", OffsetExpression = "-1h", Format = "yyyy-MM-dd" }
        };
        var expectedPages = new[]
        {
            "https://api.com/data/1/100?since=2025-01-15",
            "https://api.com/data/2/100?since=2025-01-15",
            "https://api.com/data/3/100?since=2025-01-15"
        };

        for (int page = 1; page <= 3; page++)
        {
            var staticVars = new Dictionary<string, string> { { "page", page.ToString() } };
            var result = PathTemplateResolver.ResolveWithDates(template, staticVars, dateVars, baseTime);
            result.ShouldBe(expectedPages[page - 1]);
        }
    }

    [Fact]
    public void ZeroIndexedPages_StartFromZero()
    {
        var template = "https://api.com/resource/{page}/50";

        var page0 = PathTemplateResolver.ResolveWithDates(template, new Dictionary<string, string> { { "page", "0" } }, null);
        var page1 = PathTemplateResolver.ResolveWithDates(template, new Dictionary<string, string> { { "page", "1" } }, null);
        var page2 = PathTemplateResolver.ResolveWithDates(template, new Dictionary<string, string> { { "page", "2" } }, null);

        page0.ShouldBe("https://api.com/resource/0/50");
        page1.ShouldBe("https://api.com/resource/1/50");
        page2.ShouldBe("https://api.com/resource/2/50");
    }

    [Fact]
    public void MixedPathAndQuery_CombinesBoth()
    {
        var baseTime = new DateTimeOffset(2025, 1, 15, 15, 0, 0, TimeSpan.Zero);
        var template = "https://api.com/data/{page}?limit={limit}&offset={offset}&since={lookback_date}";
        var staticVars = new Dictionary<string, string>
        {
            { "page", "3" },
            { "limit", "50" },
            { "offset", "100" }
        };
        var dateVars = new List<DateVariableConfiguration>
        {
            new() { Name = "lookback_date", OffsetExpression = "-30m", Format = "yyyy-MM-dd'T'HH:mm:ss'Z'" }
        };

        var result = PathTemplateResolver.ResolveWithDates(template, staticVars, dateVars, baseTime);

        result.ShouldBe("https://api.com/data/3?limit=50&offset=100&since=2025-01-15T14:30:00Z");
    }

    [Theory]
    [InlineData(1, "https://api.com/Patients/12345/1/50?since=2025-01-15T13:00:00Z")]
    [InlineData(2, "https://api.com/Patients/12345/2/50?since=2025-01-15T13:00:00Z")]
    [InlineData(10, "https://api.com/Patients/12345/10/50?since=2025-01-15T13:00:00Z")]
    [InlineData(100, "https://api.com/Patients/12345/100/50?since=2025-01-15T13:00:00Z")]
    public void DifferentPageNumbers_AllResolveCorrectly(int pageNumber, string expectedUrl)
    {
        var baseTime = new DateTimeOffset(2025, 1, 15, 14, 0, 0, TimeSpan.Zero);
        var template = "https://api.com/Patients/{patient_id}/{page}/{page_size}?since={lookback_date}";
        var staticVars = new Dictionary<string, string>
        {
            { "patient_id", "12345" },
            { "page", pageNumber.ToString() },
            { "page_size", "50" }
        };
        var dateVars = new List<DateVariableConfiguration>
        {
            new() { Name = "lookback_date", OffsetExpression = "-1h", Format = "yyyy-MM-dd'T'HH:mm:ss'Z'" }
        };

        var result = PathTemplateResolver.ResolveWithDates(template, staticVars, dateVars, baseTime);

        result.ShouldBe(expectedUrl);
    }
}
