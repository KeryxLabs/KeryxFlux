# Multi-Step Plugin Example

## Scenario: Epic FHIR Patient Sync

This example shows how to implement a multi-step polling workflow for syncing patients from Epic's FHIR API.

### Workflow
1. **Initial Poll**: GET `/Patient?_lastUpdated=gt2024-01-01` ? Returns list of patients
2. **For Each Patient**: GET `/Patient/{id}/Appointment` ? Get appointments
3. **For Each Patient**: GET `/Patient/{id}/MedicationRequest` ? Get medications
4. **Aggregate All Data** ? Forward complete patient bundle

---

## Plugin Implementation

```csharp
using System.Text;
using System.Text.Json;
using KeryxFlux.Contracts;

namespace KeryxFlux.Plugins.EpicPatientSync;

public class EpicPatientSyncPlugin : IMultiStepKeryxFluxPlugin
{
    public string Name => "EpicPatientSyncPlugin";
    public string Version => "1.0.0";

    // Single-step transform (fallback - not used in this example)
    public TransformationResult Transform(byte[] source, TransformationContext context)
    {
        // For multi-step plugins, you can throw or return error
        return TransformationResult.Failure("This plugin requires multi-step processing");
    }

    // Multi-step transform (main logic)
    public StepTransformationResult TransformStep(
        byte[] source,
        TransformationContext context,
        AccumulatedState accumulatedState)
    {
        var json = Encoding.UTF8.GetString(source);
        var currentStepName = context.Metadata.GetValueOrDefault("stepName", "initial");

        // Route to appropriate step handler
        return currentStepName switch
        {
            "initial" => HandleInitialPatientList(json, accumulatedState),
            "appointments" => HandlePatientAppointments(json, accumulatedState),
            "medications" => HandlePatientMedications(json, accumulatedState),
            _ => StepTransformationResult.Failure($"Unknown step: {currentStepName}", accumulatedState)
        };
    }

    private StepTransformationResult HandleInitialPatientList(string json, AccumulatedState state)
    {
        // Parse FHIR Bundle
        var bundle = JsonSerializer.Deserialize<FhirBundle>(json);
        
        if (bundle?.Entry == null || bundle.Entry.Count == 0)
        {
            // No patients - complete with empty result
            var emptyResult = new { patients = Array.Empty<object>() };
            var emptyJson = JsonSerializer.Serialize(emptyResult);
            return StepTransformationResult.Complete(
                Encoding.UTF8.GetBytes(emptyJson),
                "application/json",
                state
            );
        }

        // Store patient IDs for later steps
        var patientIds = bundle.Entry
            .Select(e => e.Resource?.Id)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList();

        state.Add("patientIds", patientIds);
        state.Add("patients", bundle.Entry.Select(e => e.Resource).ToList());

        // Create next steps: Get appointments for each patient
        var nextSteps = patientIds.Select(patientId => new NextStep
        {
            Name = $"appointments-{patientId}",
            RequestUrl = $"/Patient/{patientId}/Appointment",
            Method = "GET",
            Metadata = new Dictionary<string, string>
            {
                { "stepName", "appointments" },
                { "patientId", patientId! }
            }
        }).ToList();

        return StepTransformationResult.ContinueWithSteps(nextSteps, state);
    }

    private StepTransformationResult HandlePatientAppointments(string json, AccumulatedState state)
    {
        var patientId = /* extract from context */;
        var appointments = JsonSerializer.Deserialize<FhirBundle>(json);

        // Store appointments
        if (!state.Data.ContainsKey("appointmentsByPatient"))
        {
            state.Add("appointmentsByPatient", new Dictionary<string, object>());
        }

        var appointmentDict = state.Get<Dictionary<string, object>>("appointmentsByPatient")!;
        appointmentDict[patientId] = appointments?.Entry ?? new List<object>();

        // Check if we've received appointments for all patients
        var patientIds = state.Get<List<string>>("patientIds")!;
        if (appointmentDict.Count < patientIds.Count)
        {
            // Still waiting for more appointments
            return StepTransformationResult.ContinueWithSteps(
                Array.Empty<NextStep>(),
                state
            );
        }

        // All appointments received - now get medications
        var medicationSteps = patientIds.Select(id => new NextStep
        {
            Name = $"medications-{id}",
            RequestUrl = $"/Patient/{id}/MedicationRequest",
            Method = "GET",
            Metadata = new Dictionary<string, string>
            {
                { "stepName", "medications" },
                { "patientId", id }
            }
        }).ToList();

        return StepTransformationResult.ContinueWithSteps(medicationSteps, state);
    }

    private StepTransformationResult HandlePatientMedications(string json, AccumulatedState state)
    {
        var patientId = /* extract from context */;
        var medications = JsonSerializer.Deserialize<FhirBundle>(json);

        // Store medications
        if (!state.Data.ContainsKey("medicationsByPatient"))
        {
            state.Add("medicationsByPatient", new Dictionary<string, object>());
        }

        var medicationDict = state.Get<Dictionary<string, object>>("medicationsByPatient")!;
        medicationDict[patientId] = medications?.Entry ?? new List<object>();

        // Check if we've received medications for all patients
        var patientIds = state.Get<List<string>>("patientIds")!;
        if (medicationDict.Count < patientIds.Count)
        {
            // Still waiting for more medications
            return StepTransformationResult.ContinueWithSteps(
                Array.Empty<NextStep>(),
                state
            );
        }

        // ALL DATA COLLECTED - Create final aggregated output
        var patients = state.Get<List<object>>("patients")!;
        var appointments = state.Get<Dictionary<string, object>>("appointmentsByPatient")!;
        var medications = medicationDict;

        var finalBundle = new
        {
            resourceType = "Bundle",
            type = "collection",
            timestamp = DateTimeOffset.UtcNow,
            total = patients.Count,
            entry = patients.Select((p, i) => new
            {
                patient = p,
                appointments = appointments.GetValueOrDefault(patientIds[i], Array.Empty<object>()),
                medications = medications.GetValueOrDefault(patientIds[i], Array.Empty<object>())
            }).ToList()
        };

        var finalJson = JsonSerializer.Serialize(finalBundle, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        return StepTransformationResult.Complete(
            Encoding.UTF8.GetBytes(finalJson),
            "application/fhir+json",
            state
        );
    }
}

// Simple FHIR models
public class FhirBundle
{
    public List<FhirEntry>? Entry { get; set; }
}

public class FhirEntry
{
    public FhirResource? Resource { get; set; }
}

public class FhirResource
{
    public string? Id { get; set; }
    public string? ResourceType { get; set; }
}
```

---

## Usage in Docket YAML

```yaml
name: epic-patient-sync
version: 1.0.0
type: poller
plugin_location: ./plugins/EpicPatientSync.dll

scheduler:
  cron_expression: "0 */6 * * *"  # Every 6 hours
  queue: default
  
  server:
    name: epic-fhir
    address: https://fhir.epic.com/api/FHIR/R4
    type: http
    authentication:
      type: oauth2
      token_url: https://oauth.epic.com/token
      client_id_env: EPIC_CLIENT_ID
      client_secret_env: EPIC_CLIENT_SECRET

forwarding:
  destinations:
    - name: data-warehouse
      type: http
      url: https://warehouse.hospital.com/api/patient-bundles
      method: POST
      headers:
        Content-Type: application/fhir+json
        Authorization: Bearer ${WAREHOUSE_TOKEN}
      
      retry_policy:
        max_attempts: 3
        backoff_strategy: exponential
```

---

## Execution Flow

```
1. Scheduler triggers (every 6 hours)
   ?
2. GET /Patient?_lastUpdated=gt{last-sync}
   Returns: 3 patients
   ?
3. Plugin receives patient list
   Stores: patientIds = [p1, p2, p3]
   Returns: NextSteps = [
     GET /Patient/p1/Appointment,
     GET /Patient/p2/Appointment,
     GET /Patient/p3/Appointment
   ]
   ?
4. System executes all appointment requests
   Plugin receives each response
   Stores: appointmentsByPatient = {p1: [...], p2: [...], p3: [...]}
   When last appointment received, returns: NextSteps = [
     GET /Patient/p1/MedicationRequest,
     GET /Patient/p2/MedicationRequest,
     GET /Patient/p3/MedicationRequest
   ]
   ?
5. System executes all medication requests
   Plugin receives each response
   Stores: medicationsByPatient = {p1: [...], p2: [...], p3: [...]}
   When last medication received, returns: Complete with final bundle
   ?
6. System forwards complete bundle to data warehouse
```

---

## Key Benefits

1. **State Management**: Accumulate data across multiple API calls
2. **Parallel Execution**: All steps at same level execute concurrently
3. **Clean Plugin Code**: Plugin focuses on business logic, not HTTP plumbing
4. **Auditable**: Full execution history tracked in AccumulatedState
5. **Testable**: Can mock steps and test plugin in isolation

---

## Advanced: Pagination Support

```csharp
private StepTransformationResult HandleInitialPatientList(string json, AccumulatedState state)
{
    var bundle = JsonSerializer.Deserialize<FhirBundle>(json);
    
    // Check for pagination
    var nextPageUrl = bundle?.Link
        ?.FirstOrDefault(l => l.Relation == "next")
        ?.Url;
    
    if (!string.IsNullOrEmpty(nextPageUrl))
    {
        // More pages exist - fetch next page first
        var nextPageStep = new NextStep
        {
            Name = "patient-list-page-next",
            RequestUrl = nextPageUrl,
            Method = "GET",
            Metadata = new Dictionary<string, string>
            {
                { "stepName", "initial" },
                { "isPagedRequest", "true" }
            }
        };
        
        // Accumulate patients from this page
        var existingPatients = state.Get<List<object>>("patients") ?? new List<object>();
        existingPatients.AddRange(bundle.Entry.Select(e => e.Resource));
        state.Add("patients", existingPatients);
        
        return StepTransformationResult.ContinueWithSteps(
            new[] { nextPageStep },
            state
        );
    }
    
    // No more pages - proceed with detail requests
    // ... (rest of logic)
}
```
