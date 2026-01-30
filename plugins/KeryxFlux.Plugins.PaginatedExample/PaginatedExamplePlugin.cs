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

        var items = new List<PatientData>();

        // Handle different response structures
        if (root.TryGetProperty("entry", out var entryArray) && entryArray.ValueKind == JsonValueKind.Array)
        {
            // FHIR Bundle structure: { "entry": [ { "resource": {...} } ] }
            foreach (var entry in entryArray.EnumerateArray())
            {
                if (entry.TryGetProperty("resource", out var resource))
                {
                    var patient = ParsePatient(resource);
                    if (patient != null) items.Add(patient);
                }
            }
        }
        else if (root.TryGetProperty("data", out var dataArray) && dataArray.ValueKind == JsonValueKind.Array)
        {
            // Simple structure: { "data": [...] }
            foreach (var item in dataArray.EnumerateArray())
            {
                var patient = ParsePatient(item);
                if (patient != null) items.Add(patient);
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            // Root is array: [...]
            foreach (var item in root.EnumerateArray())
            {
                var patient = ParsePatient(item);
                if (patient != null) items.Add(patient);
            }
        }

        // Transform this page's data
        var transformedPage = new
        {
            source_system = "epic-fhir",
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
            patients = items.Select(p => new
            {
                patient_id = p.Id,
                full_name = p.Name,
                birth_date = p.BirthDate,
                gender = p.Gender,
                active = p.Active
            })
        };

        var result = JsonSerializer.SerializeToUtf8Bytes(transformedPage, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        Console.WriteLine($"[PaginatedPlugin] Transformed {items.Count} patients from page {paginationCtx.CurrentPage}");

        return TransformationResult.Success(result, "application/json");
    }

    /// <summary>
    /// Process a non-paginated single request
    /// </summary>
    private TransformationResult ProcessSingleRequest(byte[] payload, TransformationContext context)
    {
        Console.WriteLine($"[PaginatedPlugin] Processing single (non-paginated) request");

        using var doc = JsonDocument.Parse(payload);
        var allItems = new List<PatientData>();

        // Parse all items from single response
        var root = doc.RootElement;
        if (root.TryGetProperty("data", out var dataArray))
        {
            foreach (var item in dataArray.EnumerateArray())
            {
                var patient = ParsePatient(item);
                if (patient != null) allItems.Add(patient);
            }
        }

        var transformed = new
        {
            source_system = "epic-fhir",
            extracted_at = DateTimeOffset.UtcNow,
            total_patients = allItems.Count,
            docket_name = context.DocketName,
            patients = allItems.Select(p => new
            {
                patient_id = p.Id,
                full_name = p.Name
            })
        };

        var result = JsonSerializer.SerializeToUtf8Bytes(transformed);
        return TransformationResult.Success(result, "application/json");
    }

    private PatientData? ParsePatient(JsonElement element)
    {
        try
        {
            // Example FHIR Patient parsing
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

            var birthDate = element.TryGetProperty("birthDate", out var bdProp) ? bdProp.GetString() : null;
            var gender = element.TryGetProperty("gender", out var genderProp) ? genderProp.GetString() : null;
            var active = element.TryGetProperty("active", out var activeProp) && activeProp.GetBoolean();

            return new PatientData
            {
                Id = id,
                Name = name,
                BirthDate = birthDate,
                Gender = gender,
                Active = active
            };
        }
        catch
        {
            return null;
        }
    }

    private class PatientData
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? BirthDate { get; set; }
        public string? Gender { get; set; }
        public bool Active { get; set; }
    }
}

