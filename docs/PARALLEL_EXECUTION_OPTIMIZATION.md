# Parallel Execution Optimization

## ?? Performance Update

### What Changed

Implemented **item-level parallelism** while keeping **step-level sequential processing**.

---

## ?? Design Decision: One Level of Parallelism

### Why Not Two Levels?

**Initial thought:** Parallelize both items AND steps
```
Items in parallel (100 patients)
  ?
Steps in parallel (3 steps per patient)
```

**Problem:** Steps within an item are often **dependent**!

```
Step 1: Get patient appointments
  ? Result: [appt1, appt2, appt3]
  ? (Plugin looks at result to decide next steps)
Step 2: For EACH appointment, get details
  ? Create: [GET /Appointment/appt1/details, GET /Appointment/appt2/details, ...]
  ? (Plugin looks at results to decide if complete)
Step 3: Aggregate all data ? Complete()
```

**If we parallelize steps:** Plugin can't make decisions based on step results!

### Correct Design: Items in Parallel, Steps Sequential

```
???????????????????????????????????????????????????????????????
?  Process Items in Parallel                                  ?
?  ????????????????  ????????????????  ????????????????      ?
?  ? Patient 1    ?  ? Patient 2    ?  ? Patient 3    ? ...  ?
?  ?              ?  ?              ?  ?              ?      ?
?  ? Step 1       ?  ? Step 1       ?  ? Step 1       ?      ?
?  ?    ?         ?  ?    ?         ?  ?    ?         ?      ?
?  ? Step 2       ?  ? Step 2       ?  ? Step 2       ?      ?
?  ?    ?         ?  ?    ?         ?  ?    ?         ?      ?
?  ? Step 3       ?  ? Step 3       ?  ? Step 3       ?      ?
?  ?    ?         ?  ?    ?         ?  ?    ?         ?      ?
?  ? Complete     ?  ? Complete     ?  ? Complete     ?      ?
?  ????????????????  ????????????????  ????????????????      ?
???????????????????????????????????????????????????????????????
```

**Benefits:**
? Items process in parallel (100 patients at once)
? Steps within item are sequential (plugin can decide next steps)
? No threading complexity in plugin code
? No locking needed within item

---

## ?? Performance Comparison

### Scenario: 100 Patients, 3 Steps Each

Each HTTP request takes ~200ms.

#### Before (Everything Sequential)

```csharp
foreach (var item in items)           // 100 patients
{
    foreach (var step in steps)      // 3 steps
    {
        await ExecuteStepRequest(step);  // 200ms each
    }
}
```

**Total Time:**
- 100 patients × 3 steps × 200ms = **60,000ms (60 seconds)**

#### After (Items Parallel, Steps Sequential)

```csharp
// Process ALL items in parallel
await Parallel.ForEachAsync(items, async (item, ct) => 
{
    // Process steps SEQUENTIALLY within this item
    foreach (var step in steps)
    {
        await ExecuteStepRequest(step);
    }
});
```

**Total Time (16 threads):**
- 100 patients / 16 threads = 7 batches
- Each batch: 3 sequential steps = 3 × 200ms = 600ms
- Total: 7 batches × 600ms = **4,200ms (4.2 seconds)**

**Speedup: 14x faster!** ??

---

## ?? Implementation

### Item-Level Parallelism

```csharp
await Parallel.ForEachAsync(
    parseResult.Items,
    new ParallelOptions
    {
        MaxDegreeOfParallelism = Environment.ProcessorCount * 2,  // 8-core = 16 threads
        CancellationToken = cancellationToken
    },
    async (item, ct) =>
    {
        // Each item's workflow is independent
        await ProcessItemWorkflow(item, pollerPlugin, docket, correlationId, ct);
    }
);
```

### Step-Level Sequential Processing

```csharp
private async Task<int> ProcessItemWorkflow(PollingItem item, ...)
{
    var pendingSteps = item.InitialSteps.ToList();

    while (pendingSteps.Any())
    {
        var nextSteps = new List<NextStep>();

        // Process steps SEQUENTIALLY - allows conditional logic
        foreach (var step in pendingSteps)
        {
            var stepResponse = await ExecuteStepRequest(step, ...);
            var stepResult = pollerPlugin.TransformItemStep(stepResponse, ...);

            if (!stepResult.HasMoreSteps)
            {
                // Complete - forward message
                return await ForwardFinalData(...);
            }

            // IMPORTANT: Next steps can depend on THIS step's result!
            nextSteps.AddRange(stepResult.NextSteps);
        }

        pendingSteps = nextSteps;  // Continue with next batch
    }
}
```

**Key Benefit:** Plugin can examine step results and dynamically create next steps!

---

## ?? Why This Design is Correct

### Example: Conditional Step Creation

```csharp
public StepTransformationResult TransformItemStep(...)
{
    if (stepType == "appointments")
    {
        var appointments = ParseAppointments(source);
        itemState.Add("appointments", appointments);

        // Create next steps BASED ON this result
        var detailSteps = appointments.Select(appt => new NextStep
        {
            RequestUrl = $"/Appointment/{appt.Id}/details"
        }).ToList();

        return StepTransformationResult.ContinueWithSteps(detailSteps, itemState);
    }
}
```

**This only works if steps are sequential!**

If appointments and medications ran in parallel:
- Can't create appointment detail steps until appointments step completes
- Can't decide if workflow is complete until all steps finish

---

## ?? Performance Characteristics

### Real-World Scenario: Epic FHIR Patient Sync

**Configuration:**
- 500 patients updated today
- 3 API calls per patient (appointments, medications, conditions)
- Each API call: ~150ms

#### Sequential Performance

```
500 patients × 3 steps × 150ms = 225,000ms = 3.75 minutes
```

#### Parallel Items Performance (16 threads)

```
500 patients / 16 = 32 batches
3 sequential steps × 150ms = 450ms per patient
32 batches × 450ms = 14,400ms = 14.4 seconds
```

**Speedup: 16x faster!**

---

## ?? Benefits

### 1. Performance Gain

? **14-16x faster** for typical healthcare workflows
? Scales with CPU cores (more cores = more items in parallel)

### 2. Plugin Simplicity

? Plugin logic stays synchronous
? No race conditions within item
? No locking needed
? Plugin can decide next steps based on results

### 3. Correctness

? Supports conditional step creation
? Supports dependent workflows
? Plugin has full control over step sequence

### 4. Fault Isolation

? One patient fails ? others continue
? Independent item workflows
? Partial success tolerated

---

## ?? Comparison: Parallelism Strategies

| Strategy | Items | Steps | Performance | Complexity | Conditional Steps |
|----------|-------|-------|-------------|------------|-------------------|
| **Sequential** | Sequential | Sequential | 1x | Simple | ? Yes |
| **Items Parallel** (Current) | **Parallel** | Sequential | **14-16x** | **Simple** | **? Yes** |
| **Steps Parallel** (Rejected) | Sequential | Parallel | 3x | Complex | ? No |
| **Both Parallel** (Overkill) | Parallel | Parallel | 43x | Very Complex | ? No |

**Verdict:** Items parallel, steps sequential is the sweet spot!

---

## ?? Monitoring

### Key Metrics

```
- items_processed_per_second
- average_steps_per_item
- parallel_item_batches
- thread_pool_utilization
- average_item_duration
- p99_item_duration
```

### Sample Log Output

```
[INFO] Extracted 100 items from initial poll. Processing each independently in parallel.
[INFO] Starting workflow for item p1
[INFO] Starting workflow for item p2
[INFO] Executing 3 steps sequentially for item p1
[INFO] Executing step appointments-p1 for item p1 -> /Patient/p1/Appointment
[INFO] Step appointments-p1 completed. Creating 5 detail steps based on result.
[INFO] Executing 5 steps sequentially for item p1
...
[INFO] Item p1 workflow complete (8 steps). Forwarding message.
[INFO] Completed processing 100 items. 100 messages forwarded successfully.
```

---

## ? Summary

**What we do:**
- ? Parallelize items (100 patients at once)
- ? Sequential steps within item (allows conditional logic)

**What we don't do:**
- ? Parallelize steps within item (would break conditional logic)

**Result:**
- ?? 14-16x performance improvement
- ?? Plugin simplicity (no threading complexity)
- ? Supports conditional/dependent workflows
- ??? Fault isolation maintained

---

**Status**: ? **IMPLEMENTED**  
**Build**: ? **SUCCESS**  
**Performance**: ?? **14-16x FASTER**  
**Design**: ? **CORRECT FOR CONDITIONAL WORKFLOWS**

This is the right balance of performance and simplicity for healthcare integrations.
