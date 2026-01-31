using KeryxFlux.Domain.Models.Dockets;
using KeryxFlux.Domain.Utilities;
using Shouldly;
using Xunit;

namespace KeryxFlux.Domain.Tests.Integration;

public class AllVariableTypesIntegrationTests
{
    [Fact]
    public void ResolveWithDates_CombinesStaticAndDateVariables()
    {
        var baseTime = new DateTimeOffset(2025, 1, 15, 15, 0, 0, TimeSpan.Zero);
        var template = "https://api.ehr.com/{environment}/{tenant_id}/patients?since={lookback_date}&offset={offset}&limit={limit}";
        
        var staticVars = new Dictionary<string, string>
        {
            { "environment", "production" },
            { "tenant_id", "HOSP001" }
        };

        var dateVars = new List<DateVariableConfiguration>
        {
            new()
            {
                Name = "lookback_date",
                OffsetExpression = "-2h",
                Format = "yyyy-MM-dd'T'HH:mm:ss'Z'",
                Timezone = "UTC"
            }
        };

        var result = PathTemplateResolver.ResolveWithDates(template, staticVars, dateVars, baseTime);

        result.ShouldBe("https://api.ehr.com/production/HOSP001/patients?since=2025-01-15T13:00:00Z&offset={offset}&limit={limit}");
    }

    [Fact]
    public void PathBasedPagination_ResolvesVariablesInPath()
    {
        var baseTime = new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero);
        var template = "https://api.com/Patients/{tenant_id}/{page}/{page_size}?since={lookback_date}";
        
        var staticVars = new Dictionary<string, string>
        {
            { "tenant_id", "HOSP001" },
            { "page_size", "50" },
            { "page", "1" }
        };

        var dateVars = new List<DateVariableConfiguration>
        {
            new()
            {
                Name = "lookback_date",
                OffsetExpression = "-1h",
                Format = "yyyy-MM-dd",
                Timezone = "UTC"
            }
        };

        var result = PathTemplateResolver.ResolveWithDates(template, staticVars, dateVars, baseTime);

        result.ShouldBe("https://api.com/Patients/HOSP001/1/50?since=2025-01-15");
    }

    [Fact]
    public void MultiTenant_RespectsTenantSpecificDateOverrides()
    {
        var baseTime = new DateTimeOffset(2025, 1, 15, 15, 0, 0, TimeSpan.Zero);
        var template = "https://api.ehr.com/{tenant_id}/data?since={lookback_date}";

        var tenant1Vars = new Dictionary<string, string> { { "tenant_id", "MAIN_HOSPITAL" } };
        var tenant1DateVars = new List<DateVariableConfiguration>
        {
            new() { Name = "lookback_date", OffsetExpression = "-1h", Format = "yyyy-MM-dd'T'HH:mm:ss'Z'" }
        };

        var tenant2Vars = new Dictionary<string, string> { { "tenant_id", "RURAL_CLINIC" } };
        var tenant2DateVars = new List<DateVariableConfiguration>
        {
            new() { Name = "lookback_date", OffsetExpression = "-4h", Format = "yyyy-MM-dd'T'HH:mm:ss'Z'" }
        };

        var result1 = PathTemplateResolver.ResolveWithDates(template, tenant1Vars, tenant1DateVars, baseTime);
        var result2 = PathTemplateResolver.ResolveWithDates(template, tenant2Vars, tenant2DateVars, baseTime);

        result1.ShouldBe("https://api.ehr.com/MAIN_HOSPITAL/data?since=2025-01-15T14:00:00Z");
        result2.ShouldBe("https://api.ehr.com/RURAL_CLINIC/data?since=2025-01-15T11:00:00Z");
    }

    [Fact]
    public void Backfill_Handles30DayDateRange()
    {
        var baseTime = new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero);
        var template = "https://api.ehr.com/patients?start={backfill_start}&end={backfill_end}";
        
        var dateVars = new List<DateVariableConfiguration>
        {
            new() { Name = "backfill_start", OffsetExpression = "-30d", Format = "yyyy-MM-dd'T'HH:mm:ss'Z'" },
            new() { Name = "backfill_end", OffsetExpression = "0", Format = "yyyy-MM-dd'T'HH:mm:ss'Z'" }
        };

        var result = PathTemplateResolver.ResolveWithDates(template, null, dateVars, baseTime);

        result.ShouldBe("https://api.ehr.com/patients?start=2024-12-16T10:00:00Z&end=2025-01-15T10:00:00Z");
    }

    [Fact]
    public void UnixTimestampFormat_WorksWithStaticVariables()
    {
        var baseTime = new DateTimeOffset(2025, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var template = "https://api.legacy.com/{environment}/data?from={start_ts}&to={end_ts}";
        
        var staticVars = new Dictionary<string, string> { { "environment", "prod" } };
        var dateVars = new List<DateVariableConfiguration>
        {
            new() { Name = "start_ts", OffsetExpression = "-1d", Format = "unix" },
            new() { Name = "end_ts", OffsetExpression = "0", Format = "unix" }
        };

        var result = PathTemplateResolver.ResolveWithDates(template, staticVars, dateVars, baseTime);

        var expectedStartTs = new DateTimeOffset(2025, 1, 14, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        var expectedEndTs = baseTime.ToUnixTimeSeconds();
        result.ShouldBe($"https://api.legacy.com/prod/data?from={expectedStartTs}&to={expectedEndTs}");
    }

    [Fact]
    public void ComplexPath_ResolvesAllVariableTypes()
    {
        var baseTime = new DateTimeOffset(2025, 1, 15, 15, 30, 0, TimeSpan.Zero);
        var template = "{api_base}/{tenant_id}/resource/{resource_type}/{page}/{page_size}?updated_after={lookback_date}&category={category}";
        
        var staticVars = new Dictionary<string, string>
        {
            { "api_base", "https://fhir.epic.com" },
            { "tenant_id", "MAIN_HOSPITAL" },
            { "resource_type", "Observation" },
            { "page", "2" },
            { "page_size", "50" },
            { "category", "laboratory" }
        };

        var dateVars = new List<DateVariableConfiguration>
        {
            new() { Name = "lookback_date", OffsetExpression = "-2h", Format = "yyyy-MM-dd'T'HH:mm:ss'Z'" }
        };

        var result = PathTemplateResolver.ResolveWithDates(template, staticVars, dateVars, baseTime);

        result.ShouldBe("https://fhir.epic.com/MAIN_HOSPITAL/resource/Observation/2/50?updated_after=2025-01-15T13:30:00Z&category=laboratory");
    }

    [Fact]
    public void MultipleDateFormats_ResolveInSameUrl()
    {
        var baseTime = new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var template = "https://api.com/data?date_iso={date_iso}&date_compact={date_compact}&timestamp={timestamp}";
        
        var dateVars = new List<DateVariableConfiguration>
        {
            new() { Name = "date_iso", OffsetExpression = "-1d", Format = "yyyy-MM-dd'T'HH:mm:ss'Z'" },
            new() { Name = "date_compact", OffsetExpression = "-1d", Format = "yyyyMMdd" },
            new() { Name = "timestamp", OffsetExpression = "-1d", Format = "unix" }
        };

        var result = PathTemplateResolver.ResolveWithDates(template, null, dateVars, baseTime);

        var expectedDate = new DateTimeOffset(2025, 1, 14, 12, 0, 0, TimeSpan.Zero);
        var expectedTs = expectedDate.ToUnixTimeSeconds();
        result.ShouldBe($"https://api.com/data?date_iso=2025-01-14T12:00:00Z&date_compact=20250114&timestamp={expectedTs}");
    }

    [Fact]
    public void VariableResolution_IsCaseInsensitive()
    {
        var baseTime = new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero);
        var template = "https://api.com/{Environment}/data?since={LookBack_Date}&tenant={Tenant_ID}";
        
        var staticVars = new Dictionary<string, string>
        {
            { "environment", "prod" },
            { "tenant_id", "HOSP001" }
        };

        var dateVars = new List<DateVariableConfiguration>
        {
            new() { Name = "lookback_date", OffsetExpression = "-1h", Format = "yyyy-MM-dd" }
        };

        var result = PathTemplateResolver.ResolveWithDates(template, staticVars, dateVars, baseTime);

        result.ShouldBe("https://api.com/prod/data?since=2025-01-15&tenant=HOSP001");
    }

    [Fact]
    public void EmptyVariables_DoesNotThrow()
    {
        var template = "https://api.com/data?since={lookback_date}";
        var baseTime = DateTimeOffset.UtcNow;

        var result1 = PathTemplateResolver.ResolveWithDates(template, null, null, baseTime);
        var result2 = PathTemplateResolver.ResolveWithDates(template, new Dictionary<string, string>(), new List<DateVariableConfiguration>(), baseTime);

        result1.ShouldBe(template);
        result2.ShouldBe(template);
    }

    [Fact]
    public void PositiveOffset_ResolvesFutureDate()
    {
        var baseTime = new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero);
        var template = "https://api.com/schedule?execute_at={future_time}";
        
        var dateVars = new List<DateVariableConfiguration>
        {
            new() { Name = "future_time", OffsetExpression = "+2h", Format = "yyyy-MM-dd'T'HH:mm:ss'Z'" }
        };

        var result = PathTemplateResolver.ResolveWithDates(template, null, dateVars, baseTime);

        result.ShouldBe("https://api.com/schedule?execute_at=2025-01-15T12:00:00Z");
    }

    [Fact]
    public void EpicFhir_ResolvesDateRangeQuery()
    {
        var baseTime = new DateTimeOffset(2025, 1, 15, 14, 0, 0, TimeSpan.Zero);
        var template = "https://fhir.epic.com/{tenant_id}/api/FHIR/R4/Patient?_lastUpdated=ge{lookback_start}&_lastUpdated=le{lookback_end}&_count={page_size}";
        
        var staticVars = new Dictionary<string, string>
        {
            { "tenant_id", "MAIN_HOSPITAL" },
            { "page_size", "100" }
        };

        var dateVars = new List<DateVariableConfiguration>
        {
            new() { Name = "lookback_start", OffsetExpression = "-2h", Format = "yyyy-MM-dd'T'HH:mm:ss'Z'" },
            new() { Name = "lookback_end", OffsetExpression = "0", Format = "yyyy-MM-dd'T'HH:mm:ss'Z'" }
        };

        var result = PathTemplateResolver.ResolveWithDates(template, staticVars, dateVars, baseTime);

        result.ShouldBe("https://fhir.epic.com/MAIN_HOSPITAL/api/FHIR/R4/Patient?_lastUpdated=ge2025-01-15T12:00:00Z&_lastUpdated=le2025-01-15T14:00:00Z&_count=100");
    }
}
