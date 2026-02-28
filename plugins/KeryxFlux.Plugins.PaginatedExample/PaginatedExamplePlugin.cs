using KeryxFlux.Contracts;
using System.Text;
using System.Text.Json;

namespace KeryxFlux.Plugins.PaginatedExample;

/// <summary>
/// Example plugin that processes paginated API responses ONE PAGE AT A TIME.
/// Receives individual pages with pagination context from the orchestrator.
/// </summary>
public class PaginatedExamplePlugin : IReceiverPlugin
{
    public string Name => "Paginated Example Plugin";
    public string Version => "2.0.0";  // Updated for page-by-page processing

    public TransformationResult Transform(byte[] payload, TransformationContext context)
    {
        try
        {
            // Check if this is part of a paginated workflow
            if (context.Pagination != null)
            {
                return ProcessPaginatedPage(payload, context);
            }

            // Non-paginated workflow (single request)
            return ProcessSingleRequest(payload, context);
        }
        catch (Exception ex)
        {
            return TransformationResult.Failure($"Failed to transform data: {ex.Message}");
        }
    }

    /// <summary>
    /// Process a single page from a paginated workflow
    /// </summary>
    private TransformationResult ProcessPaginatedPage(byte[] payload, TransformationContext context)
    {
        var paginationCtx = context.Pagination!;
        
        Console.WriteLine($"[PaginatedPlugin] Processing page {paginationCtx.CurrentPage}" +
            (paginationCtx.TotalPages.HasValue ? $" of {paginationCtx.TotalPages}" : "") +
            $" (Strategy: {paginationCtx.Strategy})");

        // Parse this page's data (structure depends on API)
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        var items = new List<RecordData>();

        // Handle different response structures
        if (root.TryGetProperty("entry", out var entryArray) && entryArray.ValueKind == JsonValueKind.Array)
        {
            // API Bundle structure: { "entry": [ { "resource": {...} } ] }
            foreach (var entry in entryArray.EnumerateArray())
            {
                if (entry.TryGetProperty("resource", out var resource))
                {
                    var record = ParseRecord(resource);
                    if (record != null) items.Add(record);
                }
            }
        }
        else if (root.TryGetProperty("data", out var dataArray) && dataArray.ValueKind == JsonValueKind.Array)
        {
            // Simple structure: { "data": [...] }
            foreach (var item in dataArray.EnumerateArray())
            {
                var record = ParseRecord(item);
                if (record != null) items.Add(record);
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            // Root is array: [...]
            foreach (var item in root.EnumerateArray())
            {
                var record = ParseRecord(item);
                if (record != null) items.Add(record);
            }
        }

        // Transform this page's data
        var transformedPage = new
        {
            source_system = "ci-platform",
            extracted_at = DateTimeOffset.UtcNow,
            docket_name = context.DocketName,
            environment = context.DocketConfiguration.GetValueOrDefault("environment", "unknown"),
            business_id = context.DocketConfiguration.GetValueOrDefault("business_id", "unknown"),
            
            // Pagination metadata
            pagination = new
            {
                current_page = paginationCtx.CurrentPage,
                total_pages = paginationCtx.TotalPages,
                page_size = paginationCtx.PageSize,
                is_first_page = paginationCtx.IsFirstPage,
                is_last_page = paginationCtx.IsLastPage,
                strategy = paginationCtx.Strategy
            },
            
            // Actual data from this page
            page_item_count = items.Count,
            records = items.Select(r => new
            {
                record_id = r.Id,
                name = r.Name,
                created_date = r.CreatedDate,
                status = r.Status,
                active = r.Active
            })
        };

        var result = JsonSerializer.SerializeToUtf8Bytes(transformedPage, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        Console.WriteLine($"[PaginatedPlugin] Transformed {items.Count} records from page {paginationCtx.CurrentPage}");

        return TransformationResult.Success(result, "application/json");
    }

    /// <summary>
    /// Process a non-paginated single request
    /// </summary>
    private TransformationResult ProcessSingleRequest(byte[] payload, TransformationContext context)
    {
        Console.WriteLine($"[PaginatedPlugin] Processing single (non-paginated) request");

        using var doc = JsonDocument.Parse(payload);
        var allItems = new List<RecordData>();

        // Parse all items from single response
        var root = doc.RootElement;
        if (root.TryGetProperty("data", out var dataArray))
        {
            foreach (var item in dataArray.EnumerateArray())
            {
                var record = ParseRecord(item);
                if (record != null) allItems.Add(record);
            }
        }

        var transformed = new
        {
            source_system = "ci-platform",
            extracted_at = DateTimeOffset.UtcNow,
            total_records = allItems.Count,
            docket_name = context.DocketName,
            records = allItems.Select(r => new
            {
                record_id = r.Id,
                name = r.Name
            })
        };

        var result = JsonSerializer.SerializeToUtf8Bytes(transformed);
        return TransformationResult.Success(result, "application/json");
    }

    private RecordData? ParseRecord(JsonElement element)
    {
        try
        {
            // Example API record parsing
            var id = element.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
            if (string.IsNullOrEmpty(id)) return null;

            var name = "Unknown";
            if (element.TryGetProperty("name", out var nameArray) && nameArray.ValueKind == JsonValueKind.Array && nameArray.GetArrayLength() > 0)
            {
                var firstName = nameArray[0];
                var given = firstName.TryGetProperty("given", out var givenArray) && givenArray.ValueKind == JsonValueKind.Array && givenArray.GetArrayLength() > 0
                    ? givenArray[0].GetString()
                    : "";
                var family = firstName.TryGetProperty("family", out var familyProp)
                    ? familyProp.GetString()
                    : "";
                name = $"{given} {family}".Trim();
            }

            var createdDate = element.TryGetProperty("createdDate", out var cdProp) ? cdProp.GetString() : null;
            var status = element.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;
            var active = element.TryGetProperty("active", out var activeProp) && activeProp.GetBoolean();

            return new RecordData
            {
                Id = id,
                Name = name,
                CreatedDate = createdDate,
                Status = status,
                Active = active
            };
        }
        catch
        {
            return null;
        }
    }

    private class RecordData
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? CreatedDate { get; set; }
        public string? Status { get; set; }
        public bool Active { get; set; }
    }
}

