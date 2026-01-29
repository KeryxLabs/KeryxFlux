# Plugin Development Guide

## Overview

KeryxFlux uses specialized plugin interfaces based on the context where they're used:

- **`IReceiverPlugin`** - For HTTP/TCP/RabbitMQ/Kafka receivers (single-step transformations)
- **`IPollerPlugin`** - For scheduled pollers (multi-step capable by default)

---

## IReceiverPlugin: Single-Step Transformations

### When to Use
- Processing incoming HTTP requests
- Handling messages from RabbitMQ/Kafka queues
- Transforming TCP/MLLP messages
- Any scenario where you receive ONE message and transform it

### Interface

```csharp
public interface IReceiverPlugin : IKeryxFluxPlugin
{
    string Name { get; }
    string Version { get; }
    TransformationResult Transform(byte[] source, TransformationContext context);
}
```

### Example: Simple JSON Envelope Plugin

```csharp
using System.Text;
using System.Text.Json;
using KeryxFlux.Contracts;

namespace KeryxFlux.Plugins.Receivers;

public class JsonEnvelopePlugin : IReceiverPlugin
{
    public string Name => "JsonEnvelopePlugin";
    public string Version => "1.0.0";

    public TransformationResult Transform(byte[] source, TransformationContext context)
    {
        try
        {
            var input = Encoding.UTF8.GetString(source);

            // Add metadata envelope
            var envelope = new
            {
                metadata = new
                {
                    receivedAt = context.ReceivedAt,
                    docket = context.DocketName,
                    correlationId = context.CorrelationId,
                    source = context.ReceiverType
                },
                payload = JsonDocument.Parse(input).RootElement
            };

            var json = JsonSerializer.Serialize(envelope, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            return TransformationResult.Success(
                Encoding.UTF8.GetBytes(json),
                "application/json"
            );
        }
        catch (Exception ex)
        {
            return TransformationResult.Failure($"Transformation failed: {ex.Message}");
        }
    }
}
```

### Example: HL7 to JSON Plugin

```csharp
using KeryxFlux.Contracts;
using KeryxPars; // Your HL7 parsing library

namespace KeryxFlux.Plugins.Receivers;

public class Hl7ToJsonPlugin : IReceiverPlugin
{
    public string Name => "Hl7ToJsonPlugin";
    public string Version => "1.0.0";

    public TransformationResult Transform(byte[] source, TransformationContext context)
    {
        try
        {
            var hl7Message = Encoding.UTF8.GetString(source);
            
            // Parse HL7 using KeryxPars
            var parser = new Hl7Parser();
            var parsed = parser.Parse(hl7Message);

            // Convert to JSON
            var json = JsonSerializer.Serialize(new
            {
                messageType = parsed.MessageType,
                patientId = parsed.GetField("PID-3"),
                visitNumber = parsed.GetField("PV1-19"),
                admitDateTime = parsed.GetField("PV1-44"),
                segments = parsed.Segments
            });

            var metadata = new Dictionary<string, string>
            {
                { "hl7_message_type", parsed.MessageType },
                { "patient_id", parsed.GetField("PID-3") }
            };

            return TransformationResult.Success(
                Encoding.UTF8.GetBytes(json),
                "application/json",
                metadata
            );
        }
        catch (Exception ex)
        {
            return TransformationResult.Failure($"HL7 parsing failed: {ex.Message}");
        }
    }
}
```

---

## IPollerPlugin: Multi-Step Workflows

### When to Use
- Polling REST APIs with paginated results
- Fetching a list, then fetching details for each item
- Making chained API calls (patients ? appointments ? medications)
- Any scenario requiring multiple HTTP requests to build complete data

### Interface

```csharp
public interface IPollerPlugin : IKeryxFluxPlugin
{
    string Name { get; }
    string Version { get; }
    StepTransformationResult TransformStep(
        byte[] source, 
        TransformationContext context,
        AccumulatedState accumulatedState);
}
```

### Simple Single-Step Poller

Even for single-step pollers, implement `IPollerPlugin` and return `Complete()` immediately:

```csharp
using KeryxFlux.Contracts;

namespace KeryxFlux.Plugins.Pollers;

public class SimpleApiPollerPlugin : IPollerPlugin
{
    public string Name => "SimpleApiPollerPlugin";
    public string Version => "1.0.0";

    public StepTransformationResult TransformStep(
        byte[] source,
        TransformationContext context,
        AccumulatedState accumulatedState)
    {
        try
        {
            // Parse API response
            var json = Encoding.UTF8.GetString(source);
            var data = JsonSerializer.Deserialize<ApiResponse>(json);

            // Transform and return immediately (single-step)
            var transformed = new
            {
                timestamp = DateTimeOffset.UtcNow,
                items = data.Items,
                count = data.Items.Count
            };

            var output = JsonSerializer.Serialize(transformed);

            return StepTransformationResult.Complete(
                Encoding.UTF8.GetBytes(output),
                "application/json",
                accumulatedState
            );
        }
        catch (Exception ex)
        {
            return StepTransformationResult.Failure(ex.Message, accumulatedState);
        }
    }
}
```

### Multi-Step Poller: Epic Patient Sync

```csharp
using KeryxFlux.Contracts;

namespace KeryxFlux.Plugins.Pollers;

public class EpicPatientSyncPlugin : IPollerPlugin
{
    public string Name => "EpicPatientSyncPlugin";
    public string Version => "1.0.0";

    private const string STEP_INITIAL = "initial";
    private const string STEP_APPOINTMENTS = "appointments";
    private const string STEP_MEDICATIONS = "medications";

    public StepTransformationResult TransformStep(
        byte[] source,
        TransformationContext context,
        AccumulatedState accumulatedState)
    {
        var stepName = context.Metadata.GetValueOrDefault("stepName", STEP_INITIAL);

        return stepName switch
        {
            STEP_INITIAL => HandleInitialPatientList(source, context, accumulatedState),
            STEP_APPOINTMENTS => HandleAppointments(source, context, accumulatedState),
            STEP_MEDICATIONS => HandleMedications(source, context, accumulatedState),
            _ => StepTransformationResult.Failure($"Unknown step: {stepName}", accumulatedState)
        };
    }

    private StepTransformationResult HandleInitialPatientList(
        byte[] source,
        TransformationContext context,
        AccumulatedState state)
    {
        var json = Encoding.UTF8.GetString(source);
        var bundle = JsonSerializer.Deserialize<FhirBundle>(json);

        if (bundle?.Entry == null || bundle.Entry.Count == 0)
        {
            // No patients - return empty result
            return StepTransformationResult.Complete(
                Encoding.UTF8.GetBytes("{ \"patients\": [] }"),
                "application/json",
                state
            );
        }

        // Extract patient IDs
        var patientIds = bundle.Entry
            .Select(e => e.Resource?.Id)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList();

        // Store for later
        state.Add("patientIds", patientIds);
        state.Add("patients", bundle.Entry.Select(e => e.Resource).ToList());

        // Create next steps: Fetch appointments for each patient
        var appointmentSteps = patientIds.Select(id => new NextStep
        {
            Name = $"appointments-{id}",
            RequestUrl = $"/Patient/{id}/Appointment",
            Method = "GET",
            Metadata = new Dictionary<string, string>
            {
                { "stepName", STEP_APPOINTMENTS },
                { "patientId", id! }
            }
        }).ToList();

        return StepTransformationResult.ContinueWithSteps(appointmentSteps, state);
    }

    private StepTransformationResult HandleAppointments(
        byte[] source,
        TransformationContext context,
        AccumulatedState state)
    {
        var patientId = context.Metadata["patientId"];
        var json = Encoding.UTF8.GetString(source);
        var appointments = JsonSerializer.Deserialize<FhirBundle>(json);

        // Store appointments for this patient
        var appointmentDict = state.Get<Dictionary<string, object>>("appointmentsByPatient")
            ?? new Dictionary<string, object>();
        appointmentDict[patientId] = appointments?.Entry ?? new List<object>();
        state.Add("appointmentsByPatient", appointmentDict);

        // Check if we've received all appointments
        var patientIds = state.Get<List<string>>("patientIds")!;
        if (appointmentDict.Count < patientIds.Count)
        {
            // Still waiting - no more steps yet
            return StepTransformationResult.ContinueWithSteps(
                Array.Empty<NextStep>(),
                state
            );
        }

        // All appointments received - fetch medications next
        var medicationSteps = patientIds.Select(id => new NextStep
        {
            Name = $"medications-{id}",
            RequestUrl = $"/Patient/{id}/MedicationRequest",
            Method = "GET",
            Metadata = new Dictionary<string, string>
            {
                { "stepName", STEP_MEDICATIONS },
                { "patientId", id }
            }
        }).ToList();

        return StepTransformationResult.ContinueWithSteps(medicationSteps, state);
    }

    private StepTransformationResult HandleMedications(
        byte[] source,
        TransformationContext context,
        AccumulatedState state)
    {
        var patientId = context.Metadata["patientId"];
        var json = Encoding.UTF8.GetString(source);
        var medications = JsonSerializer.Deserialize<FhirBundle>(json);

        // Store medications
        var medicationDict = state.Get<Dictionary<string, object>>("medicationsByPatient")
            ?? new Dictionary<string, object>();
        medicationDict[patientId] = medications?.Entry ?? new List<object>();
        state.Add("medicationsByPatient", medicationDict);

        // Check if all collected
        var patientIds = state.Get<List<string>>("patientIds")!;
        if (medicationDict.Count < patientIds.Count)
        {
            // Still waiting
            return StepTransformationResult.ContinueWithSteps(
                Array.Empty<NextStep>(),
                state
            );
        }

        // ALL DATA COLLECTED - Create final bundle
        var patients = state.Get<List<object>>("patients")!;
        var appointments = state.Get<Dictionary<string, object>>("appointmentsByPatient")!;
        var medications = medicationDict;

        var finalBundle = new
        {
            resourceType = "Bundle",
            type = "collection",
            timestamp = DateTimeOffset.UtcNow,
            totalPatients = patients.Count,
            entry = patients.Select((p, i) =>
            {
                var pid = patientIds[i];
                return new
                {
                    patient = p,
                    appointments = appointments.GetValueOrDefault(pid, Array.Empty<object>()),
                    medications = medications.GetValueOrDefault(pid, Array.Empty<object>())
                };
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
```

---

## Key Differences

| Aspect | IReceiverPlugin | IPollerPlugin |
|--------|----------------|---------------|
| **Method** | `Transform()` | `TransformStep()` |
| **State** | None (stateless) | `AccumulatedState` across steps |
| **Steps** | Always single-step | Multi-step capable |
| **Use Case** | Incoming messages | API polling |
| **Result Type** | `TransformationResult` | `StepTransformationResult` |
| **Complexity** | Simple | Can be complex |

---

## Building & Testing

### Build Plugin

```bash
dotnet new classlib -n MyPlugin -f net8.0
dotnet add reference path/to/KeryxFlux.Contracts.csproj
# Implement IReceiverPlugin or IPollerPlugin
dotnet build
```

### Test Plugin

```csharp
[Fact]
public void ReceiverPlugin_Transform_Success()
{
    var plugin = new JsonEnvelopePlugin();
    var source = Encoding.UTF8.GetBytes("{\"test\": \"data\"}");
    var context = TransformationContext.Create("test-docket", "http", "test-123");

    var result = plugin.Transform(source, context);

    Assert.True(result.IsSuccess);
    Assert.Contains("test", Encoding.UTF8.GetString(result.Data!));
}

[Fact]
public void PollerPlugin_SingleStep_ReturnsComplete()
{
    var plugin = new SimpleApiPollerPlugin();
    var source = Encoding.UTF8.GetBytes("{\"items\": [1, 2, 3]}");
    var context = TransformationContext.Create("test-docket", "poller", "test-123");
    var state = new AccumulatedState();

    var result = plugin.TransformStep(source, context, state);

    Assert.True(result.IsSuccess);
    Assert.False(result.HasMoreSteps);
    Assert.NotNull(result.FinalData);
}
```

---

## Next Steps

- See [PHASE_1_GUIDE.md](../PHASE_1_GUIDE.md) for creating your first plugin
- See [MULTI_STEP_ARCHITECTURE.md](./MULTI_STEP_ARCHITECTURE.md) for advanced multi-step patterns
- Check `dockets/examples/` for YAML configuration examples
