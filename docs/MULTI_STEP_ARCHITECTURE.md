# Multi-Step Polling: Architecture & Implementation

## ?? Problem Solved

Healthcare integrations often require **chained API calls**:

1. **Initial poll** returns a list (patients, orders, appointments)
2. **For each item** in list, make additional API calls to get details
3. **Aggregate all data** into a single cohesive message
4. **Forward once** to destination

### Real-World Example: Epic FHIR Patient Sync

```
GET /Patient?_lastUpdated=gt2024-01-01
  ? Returns: [patient1, patient2, patient3]
  ?
For each patient:
  GET /Patient/{id}/Appointment
  GET /Patient/{id}/MedicationRequest
  GET /Patient/{id}/Condition
  ?
Aggregate: {
  patients: [...],
  appointments: [...],
  medications: [...],
  conditions: [...]
}
  ?
POST to Data Warehouse (single message with ALL data)
```

---

## ??? Architecture

### New Contracts

#### **`IMultiStepKeryxFluxPlugin`**
Extends `IKeryxFluxPlugin` with multi-step support:

```csharp
public interface IMultiStepKeryxFluxPlugin : IKeryxFluxPlugin
{
    StepTransformationResult TransformStep(
        byte[] source,
        TransformationContext context,
        AccumulatedState accumulatedState);
}
```

#### **`AccumulatedState`**
Carries data across steps:

```csharp
public sealed class AccumulatedState
{
    public Dictionary<string, object> Data { get; init; }
    public List<StepExecution> ExecutionHistory { get; init; }
    public int CurrentStepIndex => ExecutionHistory.Count;
    
    public void Add(string key, object value);
    public T? Get<T>(string key);
}
```

#### **`StepTransformationResult`**
Indicates continuation or completion:

```csharp
public sealed class StepTransformationResult
{
    public bool HasMoreSteps { get; }
    public IReadOnlyList<NextStep> NextSteps { get; }
    public byte[]? FinalData { get; }  // Only when HasMoreSteps = false
    public AccumulatedState AccumulatedState { get; }
    
    // Factory methods
    static ContinueWithSteps(IReadOnlyList<NextStep> nextSteps, AccumulatedState state);
    static Complete(byte[] finalData, string contentType, AccumulatedState state);
    static Failure(string errorMessage, AccumulatedState state);
}
```

#### **`NextStep`**
Describes the next API call to make:

```csharp
public sealed class NextStep
{
    public string Name { get; init; }
    public string RequestUrl { get; init; }
    public string Method { get; init; } = "GET";
    public byte[]? RequestBody { get; init; }
    public Dictionary<string, string>? Headers { get; init; }
    public Dictionary<string, string> Metadata { get; init; }
}
```

---

## ?? Execution Flow

```
1. Poller triggers
   ?
2. Make initial HTTP request
   ?
3. Plugin.TransformStep(response, context, new AccumulatedState())
   ?
4. Plugin parses response, extracts patient IDs
   Stores: state.Add("patientIds", [p1, p2, p3])
   Returns: ContinueWithSteps([
     NextStep{ RequestUrl = "/Patient/p1/Appointment" },
     NextStep{ RequestUrl = "/Patient/p2/Appointment" },
     NextStep{ RequestUrl = "/Patient/p3/Appointment" }
   ], state)
   ?
5. Handler makes ALL 3 requests in parallel
   ?
6. For EACH response:
   Plugin.TransformStep(response, context, accumulatedState)
   Stores: state.Add("appointments-p1", data)
   ?
7. When LAST appointment received:
   Plugin checks: state.Data.ContainsKey("appointments-p1/p2/p3")
   Returns: ContinueWithSteps([
     NextStep{ RequestUrl = "/Patient/p1/MedicationRequest" },
     ...
   ], state)
   ?
8. Repeat for medications
   ?
9. When ALL data collected:
   Plugin aggregates everything
   Returns: Complete(finalBundle, "application/json", state)
   ?
10. Handler forwards final bundle to destinations
```

---

## ?? Plugin Implementation Pattern

### Step 1: Define Step Names

```csharp
private const string STEP_INITIAL = "initial";
private const string STEP_APPOINTMENTS = "appointments";
private const string STEP_MEDICATIONS = "medications";
```

### Step 2: Route in TransformStep

```csharp
public StepTransformationResult TransformStep(
    byte[] source,
    TransformationContext context,
    AccumulatedState accumulatedState)
{
    var stepName = context.Metadata.GetValueOrDefault("stepName", STEP_INITIAL);
    
    return stepName switch
    {
        STEP_INITIAL => HandleInitialList(source, accumulatedState),
        STEP_APPOINTMENTS => HandleAppointments(source, accumulatedState),
        STEP_MEDICATIONS => HandleMedications(source, accumulatedState),
        _ => StepTransformationResult.Failure($"Unknown step: {stepName}", accumulatedState)
    };
}
```

### Step 3: Handle Initial Step

```csharp
private StepTransformationResult HandleInitialList(byte[] source, AccumulatedState state)
{
    var json = Encoding.UTF8.GetString(source);
    var patients = JsonSerializer.Deserialize<PatientList>(json);
    
    if (patients.Items.Count == 0)
    {
        // No patients - return empty result
        return StepTransformationResult.Complete(
            Encoding.UTF8.GetBytes("{}"),
            "application/json",
            state
        );
    }
    
    // Store patient IDs
    var patientIds = patients.Items.Select(p => p.Id).ToList();
    state.Add("patientIds", patientIds);
    state.Add("patients", patients.Items);
    
    // Create next steps
    var nextSteps = patientIds.Select(id => new NextStep
    {
        Name = $"appointments-{id}",
        RequestUrl = $"/Patient/{id}/Appointment",
        Metadata = new Dictionary<string, string>
        {
            { "stepName", STEP_APPOINTMENTS },
            { "patientId", id }
        }
    }).ToList();
    
    return StepTransformationResult.ContinueWithSteps(nextSteps, state);
}
```

### Step 4: Handle Intermediate Steps

```csharp
private StepTransformationResult HandleAppointments(byte[] source, AccumulatedState state)
{
    var json = Encoding.UTF8.GetString(source);
    var appointments = JsonSerializer.Deserialize<AppointmentList>(json);
    
    // Extract patient ID from metadata (passed from previous step)
    var patientId = /* get from context */;
    
    // Store appointments for this patient
    if (!state.Data.ContainsKey("appointmentsByPatient"))
    {
        state.Add("appointmentsByPatient", new Dictionary<string, object>());
    }
    var appointmentDict = state.Get<Dictionary<string, object>>("appointmentsByPatient")!;
    appointmentDict[patientId] = appointments;
    
    // Check if we've received ALL appointments
    var patientIds = state.Get<List<string>>("patientIds")!;
    if (appointmentDict.Count < patientIds.Count)
    {
        // Still waiting for more - return empty next steps
        return StepTransformationResult.ContinueWithSteps(
            Array.Empty<NextStep>(),
            state
        );
    }
    
    // All appointments received - proceed to medications
    var medicationSteps = patientIds.Select(id => new NextStep
    {
        Name = $"medications-{id}",
        RequestUrl = $"/Patient/{id}/MedicationRequest",
        Metadata = new Dictionary<string, string>
        {
            { "stepName", STEP_MEDICATIONS },
            { "patientId", id }
        }
    }).ToList();
    
    return StepTransformationResult.ContinueWithSteps(medicationSteps, state);
}
```

### Step 5: Complete When All Data Collected

```csharp
private StepTransformationResult HandleMedications(byte[] source, AccumulatedState state)
{
    // Store medication data...
    // Check if all collected...
    
    if (allDataCollected)
    {
        // Aggregate everything
        var patients = state.Get<List<Patient>>("patients")!;
        var appointments = state.Get<Dictionary<string, object>>("appointmentsByPatient")!;
        var medications = state.Get<Dictionary<string, object>>("medicationsByPatient")!;
        
        var finalBundle = new
        {
            resourceType = "Bundle",
            timestamp = DateTimeOffset.UtcNow,
            patients = patients.Select((p, i) => new
            {
                patient = p,
                appointments = appointments[p.Id],
                medications = medications[p.Id]
            }).ToList()
        };
        
        var json = JsonSerializer.Serialize(finalBundle);
        return StepTransformationResult.Complete(
            Encoding.UTF8.GetBytes(json),
            "application/json",
            state
        );
    }
    
    return StepTransformationResult.ContinueWithSteps(Array.Empty<NextStep>(), state);
}
```

---

## ?? Key Design Patterns

### Pattern 1: Parallel Fan-Out

```csharp
// Create steps for ALL patients at once
var nextSteps = patientIds.Select(id => new NextStep
{
    Name = $"details-{id}",
    RequestUrl = $"/Patient/{id}"
}).ToList();

// Handler will execute ALL requests in parallel
return StepTransformationResult.ContinueWithSteps(nextSteps, state);
```

### Pattern 2: Counting Completion

```csharp
// Store expected count
state.Add("expectedCount", patientIds.Count);

// In each intermediate step
var dict = state.Get<Dictionary<string, object>>("results")!;
dict[patientId] = data;

if (dict.Count == state.Get<int>("expectedCount"))
{
    // All collected - proceed to next phase or complete
}
```

### Pattern 3: Pagination Support

```csharp
private StepTransformationResult HandlePagedList(byte[] source, AccumulatedState state)
{
    var response = JsonSerializer.Deserialize<PagedResponse>(source);
    
    // Accumulate items from this page
    var allItems = state.Get<List<object>>("allItems") ?? new List<object>();
    allItems.AddRange(response.Items);
    state.Add("allItems", allItems);
    
    // Check for next page
    if (!string.IsNullOrEmpty(response.NextPageUrl))
    {
        var nextPageStep = new NextStep
        {
            Name = "next-page",
            RequestUrl = response.NextPageUrl,
            Metadata = new Dictionary<string, string>
            {
                { "stepName", STEP_INITIAL },
                { "isPaged", "true" }
            }
        };
        
        return StepTransformationResult.ContinueWithSteps(
            new[] { nextPageStep },
            state
        );
    }
    
    // No more pages - proceed with detail requests
    return CreateDetailSteps(allItems, state);
}
```

---

## ?? Handler Logic

The `ProcessMultiStepCommandHandler` handles:

1. **Plugin Detection**: Checks if plugin implements `IMultiStepKeryxFluxPlugin`
2. **State Management**: Passes `AccumulatedState` through all steps
3. **HTTP Execution**: Makes requests for all `NextStep` items
4. **Recursion**: Calls itself for each step response
5. **Forwarding**: Only forwards when `HasMoreSteps = false`

```csharp
// In Handler
if (stepResult.HasMoreSteps)
{
    await ExecuteNextSteps(stepResult.NextSteps, docket, correlationId, stepResult.AccumulatedState, cancellationToken);
    return ProcessMessageResult.Success(0); // Don't forward yet
}
else
{
    // All steps complete - forward final data
    var forwardedCount = await ForwardFinalData(
        stepResult.FinalData!,
        stepResult.ContentType!,
        docket,
        correlationId,
        cancellationToken);
    return ProcessMessageResult.Success(forwardedCount);
}
```

---

## ?? Testing Strategy

### Unit Test: Single Plugin Step

```csharp
[Fact]
public void TransformStep_InitialList_ReturnsNextSteps()
{
    var plugin = new EpicPatientSyncPlugin();
    var state = new AccumulatedState();
    var source = Encoding.UTF8.GetBytes(@"{ ""entry"": [{""id"": ""p1""}, {""id"": ""p2""}] }");
    
    var result = plugin.TransformStep(source, CreateContext(), state);
    
    Assert.True(result.IsSuccess);
    Assert.True(result.HasMoreSteps);
    Assert.Equal(2, result.NextSteps.Count);
    Assert.Contains(result.NextSteps, s => s.RequestUrl.Contains("/Patient/p1"));
}
```

### Integration Test: Full Workflow

```csharp
[Fact]
public async Task ProcessMultiStep_CompleteWorkflow_ForwardsAggregatedData()
{
    // Arrange
    var mockHttpClient = CreateMockHttp(
        ("/Patient", "{ \"entry\": [{\"id\": \"p1\"}] }"),
        ("/Patient/p1/Appointment", "{ \"entry\": [...] }"),
        ("/Patient/p1/MedicationRequest", "{ \"entry\": [...] }")
    );
    
    // Act
    var result = await _handler.Handle(new ProcessMultiStepCommand
    {
        DocketName = "test-docket",
        Message = initialMessage
    }, CancellationToken.None);
    
    // Assert
    Assert.True(result.IsSuccess);
    Assert.Equal(1, result.DestinationsForwarded);
    _mockSender.Verify(s => s.SendAsync(
        It.Is<OutboundMessage>(m => m.Payload.Contains("appointments")),
        It.IsAny<CancellationToken>()
    ));
}
```

---

## ?? Performance Considerations

### Parallel Execution
- All steps at same level execute **concurrently**
- If 10 patients, all 10 requests happen in parallel
- Use semaphore if you need rate limiting

### Memory Management
- `AccumulatedState` holds all data in memory
- For large datasets (1000+ patients), consider:
  - Streaming to temp storage
  - Batch processing (chunk into groups)
  - Pagination limits

### Timeout Handling
- Each step has its own timeout
- Consider increasing docket-level timeout for multi-step workflows

---

## ?? Usage in Docket YAML

```yaml
name: epic-patient-full-sync
version: 1.0.0
type: poller
plugin_location: ./plugins/EpicPatientSync.dll

scheduler:
  cron_expression: "0 2 * * *"  # 2 AM daily
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
    timeout_seconds: 300  # 5 minutes for full workflow

forwarding:
  destinations:
    - name: data-lake
      type: http
      url: https://datalake.hospital.com/api/patient-bundles
      method: POST
      timeout_seconds: 60
      
      retry_policy:
        max_attempts: 3
        backoff_strategy: exponential

telemetry:
  trace_requests: true
  log_level: Information
  metrics:
    - step_execution_duration
    - steps_per_workflow
    - aggregated_records_count
```

---

## ? Benefits of This Design

1. **Clean Separation**: Plugin focuses on business logic, handler manages HTTP
2. **Testable**: Can test plugins without real HTTP calls
3. **Auditable**: `ExecutionHistory` tracks every step
4. **Flexible**: Supports pagination, conditional branching, dynamic step creation
5. **Performant**: Parallel execution at each level
6. **Type-Safe**: Strong typing with `AccumulatedState.Get<T>()`
7. **Debuggable**: Clear step names and metadata

---

## ?? Related Documentation

- [PHASE_1_GUIDE.md](../PHASE_1_GUIDE.md) - Basic implementation
- [docs/MULTI_STEP_PLUGIN_GUIDE.md](./MULTI_STEP_PLUGIN_GUIDE.md) - Plugin examples
- Domain Models: `Step.cs`, `RequestUrl.cs` (legacy support)

---

**Status**: ? **IMPLEMENTED & READY FOR USE**  
**Build**: ? **SUCCESS**  
**Next**: Implement first multi-step plugin in Phase 1
