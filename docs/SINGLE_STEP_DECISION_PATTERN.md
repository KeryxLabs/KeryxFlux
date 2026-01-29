# Single-Step Decision Pattern

## ?? The Insight

**User:** "The plugin should take the current step and state, do logic, and return the NEXT step (or complete). One step at a time!"

This is **brilliant** because:
1. Plugin thinks linearly: "current step ? next step"
2. No need to track "have I collected all data?" across steps
3. Each call is simple: process response ? decide what's next

---

## ? New Design

### Plugin Flow

```
ParseInitialResponse(response)
  ? Extract items
  ? For each item: Create PollingItem with InitialStep

For each item:
  currentStep = item.InitialStep
  
  while (currentStep != null):
    response = ExecuteRequest(currentStep)
    result = plugin.TransformItemStep(currentStep, response, state, itemId)
    
    if (result.IsComplete):
      Forward(result.FinalData)
      break
    
    currentStep = result.ContinuationStep  // Next step or null
```

---

## ?? Plugin Implementation

### Example: Epic FHIR Patient Sync

```csharp
using System.Text;
using System.Text.Json;
using KeryxFlux.Contracts;

namespace KeryxFlux.Plugins.Pollers;

public class EpicPatientSyncPlugin : IPollerPlugin
{
    public string Name => "EpicPatientSyncPlugin";
    public string Version => "2.0.0";

    // PHASE 1: Parse initial list ? Create items
    public InitialPollingResult ParseInitialResponse(byte[] source, TransformationContext context)
    {
        try
        {
            var json = Encoding.UTF8.GetString(source);
            var bundle = JsonSerializer.Deserialize<FhirBundle>(json);

            if (bundle?.Entry == null || bundle.Entry.Count == 0)
            {
                return InitialPollingResult.Empty();
            }

            // Extract each patient as an independent item
            var items = bundle.Entry.Select(entry => new PollingItem
            {
                ItemId = entry.Resource.Id,
                ItemData = entry.Resource,
                
                // Start with: GET /Patient/{id}
                InitialStep = new NextStep
                {
                    Name = $"get-patient-{entry.Resource.Id}",
                    RequestUrl = $"/Patient/{entry.Resource.Id}",
                    Method = "GET",
                    Metadata = new Dictionary<string, string>
                    {
                        { "stepType", "patient" },
                        { "patientId", entry.Resource.Id }
                    }
                }
            }).ToList();

            return InitialPollingResult.Success(items);
        }
        catch (Exception ex)
        {
            return InitialPollingResult.Failure($"Failed to parse: {ex.Message}");
        }
    }

    // PHASE 2: Process each step ? Decide next step
    public StepTransformationResult TransformItemStep(
        NextStep currentStep,      // The step that just executed
        byte[] stepResponse,        // Response from that step
        AccumulatedState itemState, // This item's state
        string itemId)
    {
        try
        {
            var json = Encoding.UTF8.GetString(stepResponse);
            var stepType = currentStep.Metadata.GetValueOrDefault("stepType", "unknown");

            switch (stepType)
            {
                case "patient":
                    return HandlePatientStep(json, itemId, itemState);

                case "appointments":
                    return HandleAppointmentsStep(json, itemId, itemState);

                case "medications":
                    return HandleMedicationsStep(json, itemId, itemState);

                case "conditions":
                    return HandleConditionsStep(json, itemId, itemState);

                default:
                    return StepTransformationResult.Failure($"Unknown step type: {stepType}");
            }
        }
        catch (Exception ex)
        {
            return StepTransformationResult.Failure($"Step failed: {ex.Message}");
        }
    }

    private StepTransformationResult HandlePatientStep(string json, string itemId, AccumulatedState state)
    {
        var patient = JsonSerializer.Deserialize<PatientResource>(json);
        state.Add("patient", patient);

        // Next: Get appointments
        return StepTransformationResult.ContinueWith(new NextStep
        {
            Name = $"get-appointments-{itemId}",
            RequestUrl = $"/Patient/{itemId}/Appointment",
            Method = "GET",
            Metadata = new Dictionary<string, string>
            {
                { "stepType", "appointments" },
                { "patientId", itemId }
            }
        });
    }

    private StepTransformationResult HandleAppointmentsStep(string json, string itemId, AccumulatedState state)
    {
        var appointments = JsonSerializer.Deserialize<FhirBundle>(json);
        state.Add("appointments", appointments?.Entry ?? new List<object>());

        // Next: Get medications
        return StepTransformationResult.ContinueWith(new NextStep
        {
            Name = $"get-medications-{itemId}",
            RequestUrl = $"/Patient/{itemId}/MedicationRequest",
            Method = "GET",
            Metadata = new Dictionary<string, string>
            {
                { "stepType", "medications" },
                { "patientId", itemId }
            }
        });
    }

    private StepTransformationResult HandleMedicationsStep(string json, string itemId, AccumulatedState state)
    {
        var medications = JsonSerializer.Deserialize<FhirBundle>(json);
        state.Add("medications", medications?.Entry ?? new List<object>());

        // Next: Get conditions
        return StepTransformationResult.ContinueWith(new NextStep
        {
            Name = $"get-conditions-{itemId}",
            RequestUrl = $"/Patient/{itemId}/Condition",
            Method = "GET",
            Metadata = new Dictionary<string, string>
            {
                { "stepType", "conditions" },
                { "patientId", itemId }
            }
        });
    }

    private StepTransformationResult HandleConditionsStep(string json, string itemId, AccumulatedState state)
    {
        var conditions = JsonSerializer.Deserialize<FhirBundle>(json);
        state.Add("conditions", conditions?.Entry ?? new List<object>());

        // ALL DONE - Create final bundle
        var finalBundle = new
        {
            resourceType = "Bundle",
            type = "collection",
            timestamp = DateTimeOffset.UtcNow,
            subject = new { reference = $"Patient/{itemId}" },
            patient = state.Get<object>("patient"),
            appointments = state.Get<object>("appointments"),
            medications = state.Get<object>("medications"),
            conditions = state.Get<object>("conditions")
        };

        var finalJson = JsonSerializer.Serialize(finalBundle, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        return StepTransformationResult.Complete(
            Encoding.UTF8.GetBytes(finalJson),
            "application/fhir+json"
        );
    }
}
```

---

## ?? Key Benefits

### 1. Linear Thinking

Plugin developer thinks:
- "I just got patient data ? Next, I need appointments"
- "I just got appointments ? Next, I need medications"
- "I just got conditions ? I'm done, create final bundle"

**No complex state tracking!**

### 2. Clear Step Progression

```
Step 1: GET /Patient/p1          ? ContinueWith(GET /Patient/p1/Appointment)
Step 2: GET /Patient/p1/Appointment ? ContinueWith(GET /Patient/p1/Medication)
Step 3: GET /Patient/p1/Medication  ? ContinueWith(GET /Patient/p1/Condition)
Step 4: GET /Patient/p1/Condition   ? Complete(bundle)
```

Each step knows exactly what comes next!

### 3. Conditional Branching

```csharp
private StepTransformationResult HandleAppointmentsStep(string json, string itemId, AccumulatedState state)
{
    var appointments = JsonSerializer.Deserialize<AppointmentBundle>(json);
    state.Add("appointments", appointments);

    // Conditional: If patient has appointments, get appointment details
    if (appointments.Total > 0)
    {
        var firstApptId = appointments.Entry[0].Resource.Id;
        return StepTransformationResult.ContinueWith(new NextStep
        {
            RequestUrl = $"/Appointment/{firstApptId}/details"
        });
    }
    else
    {
        // No appointments - skip to next step
        return StepTransformationResult.ContinueWith(new NextStep
        {
            RequestUrl = $"/Patient/{itemId}/MedicationRequest"
        });
    }
}
```

### 4. Pagination Support

```csharp
private StepTransformationResult HandlePatientListPage(string json, AccumulatedState state)
{
    var bundle = JsonSerializer.Deserialize<FhirBundle>(json);
    
    // Accumulate patients from this page
    var existingPatients = state.Get<List<object>>("allPatients") ?? new List<object>();
    existingPatients.AddRange(bundle.Entry.Select(e => e.Resource));
    state.Add("allPatients", existingPatients);
    
    // Check for next page
    var nextPageUrl = bundle.Link?.FirstOrDefault(l => l.Relation == "next")?.Url;
    
    if (!string.IsNullOrEmpty(nextPageUrl))
    {
        // More pages - continue pagination
        return StepTransformationResult.ContinueWith(new NextStep
        {
            RequestUrl = nextPageUrl
        });
    }
    else
    {
        // All pages collected - complete
        var finalData = JsonSerializer.Serialize(state.Get<List<object>>("allPatients"));
        return StepTransformationResult.Complete(
            Encoding.UTF8.GetBytes(finalData),
            "application/json"
        );
    }
}
```

---

## ?? Comparison

### Old Way (Batch Steps)

```csharp
public StepTransformationResult TransformItemStep(...)
{
    if (stepType == "initial")
    {
        // Return ALL next steps at once
        return ContinueWithSteps([
            new NextStep { Url = "/appointments" },
            new NextStep { Url = "/medications" },
            new NextStep { Url = "/conditions" }
        ]);
    }
    
    // Now track: have I received all 3 responses?
    if (state.Data.ContainsKey("appointments") && 
        state.Data.ContainsKey("medications") &&
        state.Data.ContainsKey("conditions"))
    {
        return Complete(finalData);
    }
    
    return ContinueWithSteps(Array.Empty<NextStep>());
}
```

**Problems:**
- Need to track completion state
- Hard to follow logic flow
- Can't decide steps based on previous results

### New Way (Single Step)

```csharp
public StepTransformationResult TransformItemStep(NextStep currentStep, ...)
{
    switch (currentStep.Metadata["stepType"])
    {
        case "patient":
            state.Add("patient", data);
            return ContinueWith(GET /appointments);  // One next step
        
        case "appointments":
            state.Add("appointments", data);
            return ContinueWith(GET /medications);   // One next step
        
        case "medications":
            state.Add("medications", data);
            return ContinueWith(GET /conditions);    // One next step
        
        case "conditions":
            state.Add("conditions", data);
            return Complete(CreateBundle(state));    // Done!
    }
}
```

**Benefits:**
- Linear flow: step ? next step
- No completion tracking needed
- Clear progression
- Can decide based on data

---

## ? Summary

**What changed:**
- `ContinueWithSteps(List<NextStep>)` ? `ContinueWith(NextStep)`
- Plugin parameter: `byte[] stepResponse` ? `NextStep currentStep, byte[] stepResponse`
- Result: `HasMoreSteps + NextSteps[]` ? `IsComplete + ContinuationStep?`

**Why it's better:**
- ? Plugin thinks linearly (one step at a time)
- ? No need to track "have I got all data?"
- ? Clear step progression
- ? Easier conditional logic
- ? Simpler pagination
- ? Less cognitive load for developers

---

**Status**: ? **IMPLEMENTED**  
**Build**: ? **SUCCESS**  
**Simplicity**: ?? **MAXIMUM**

This is the final, cleanest design for multi-step polling!
