# Final Architectural Refinement: Catch-All Routing

## ?? The Problem

**DocketOrchestrationService was still coupled to ASP.NET Core:**
```csharp
// Before - tight coupling!
public void Initialize(WebApplication app)
{
    _docketMonitor.OnLoaded += (sender, e) => HandleDocketLoaded(app, e);
    // ...
    httpReceiver.RegisterEndpoint(app, docket.Name, endpoint);
}
```

**Issues:**
1. ? Orchestration service depends on `WebApplication`
2. ? Dynamically registering endpoints is complex
3. ? No clean way to unregister endpoints
4. ? Tight coupling to ASP.NET Core infrastructure

---

## ? The Solution: Catch-All Route + DocketManager

**Key Insight:** Dockets are just data in `DocketManager`. Let routing query the manager!

### Architecture

```
Request: POST /receive/sample
  ?
Catch-all route: MapPost("/{**path}")
  ?
Query DocketManager.GetReceiverDockets()
  ?
Find matching docket by path
  ?
Process via MediatR pipeline
  ?
Return response
```

---

## ?? What Changed

### 1. DocketOrchestrationService - Pure Coordination

**Before:**
```csharp
public class DocketOrchestrationService
{
    private readonly IDocketMonitor _docketMonitor;
    private readonly IReceiver _httpReceiver;  // ? Infrastructure dependency

    public void Initialize(WebApplication app)  // ? ASP.NET dependency
    {
        _docketMonitor.OnLoaded += (sender, e) => HandleDocketLoaded(app, e);
        // Register endpoints dynamically...
        httpReceiver.RegisterEndpoint(app, docket.Name, endpoint);
    }
}
```

**After:**
```csharp
public class DocketOrchestrationService
{
    private readonly IDocketMonitor _docketMonitor;  // ? Only needs monitor

    public void Initialize()  // ? No web framework dependency!
    {
        _docketMonitor.OnLoaded += (sender, e) => HandleDocketLoaded(e);
        // Just log - dockets are in DocketManager automatically
    }
}
```

**Benefits:**
? No ASP.NET Core dependency  
? No Infrastructure layer dependency  
? Pure orchestration - just coordinates file monitoring  
? Testable without web framework  

---

### 2. Program.cs - Catch-All Routing

**Before:**
```csharp
// Endpoints registered dynamically per docket
httpReceiver?.RegisterEndpoint(app, "sample-receiver", "/receive/sample");
httpReceiver?.RegisterEndpoint(app, "epic-sync", "/receive/epic-sync");
// etc...
```

**After:**
```csharp
// Single catch-all route
app.MapPost("/{**path}", async (
    string path,
    HttpContext context,
    IDocketManager docketManager,  // ? Query the manager!
    IMediator mediator) =>
{
    // Find docket matching this path
    var docket = docketManager.GetReceiverDockets()
        .FirstOrDefault(d => d.Receiver?.Endpoint?.TrimStart('/') == path ||
                            $"receive/{d.Name}" == path);

    if (docket == null)
    {
        return Results.NotFound(new { error = "No receiver configured for this path" });
    }

    // Process via MediatR
    var command = new ProcessMessageCommand { DocketName = docket.Name, Message = message };
    var result = await mediator.Send(command);

    return Results.Accepted(/* ... */);
});
```

**Benefits:**
? One route handles all dockets  
? DocketManager is source of truth  
? No dynamic endpoint registration complexity  
? Hot reload works automatically (docket added/removed from manager)  
? Clean separation: routing logic in Host, docket data in Domain  

---

## ?? Architectural Principles

### 1. Data vs Behavior Separation

**Dockets = Data** (in DocketManager)  
**Routing = Behavior** (in Program.cs)  

The routing queries the data, rather than the data controlling the routing.

### 2. Single Source of Truth

**DocketManager** is the single source of truth for loaded dockets.

? **Before:** Dockets in DocketManager + Endpoints registered with ASP.NET  
? **After:** Dockets in DocketManager, routing queries it  

### 3. Proper Layering

```
Host (Program.cs)
  ? queries
DocketManager (Application layer)
  ? returns
Docket data (Domain)
```

No coupling backwards - Host queries Domain/Application, not vice versa.

### 4. Framework Decoupling

**OrchestrationService no longer knows about:**
- ? WebApplication
- ? ASP.NET Core
- ? HttpReceiver
- ? Endpoint registration

**It only knows about:**
- ? IDocketMonitor (file watching)
- ? Logging

---

## ?? Comparison

### DocketOrchestrationService

| Aspect | Before | After |
|--------|--------|-------|
| **Dependencies** | Monitor, Receiver, WebApp | Monitor only |
| **Responsibilities** | Monitor + Endpoint registration | Monitor only |
| **ASP.NET coupling** | ? Yes | ? No |
| **Initialize() params** | ? WebApplication | ? None |
| **Testability** | ? Requires mock WebApp | ? Easy - mock Monitor |

### Routing

| Aspect | Before | After |
|--------|--------|-------|
| **Endpoints per docket** | ? Dynamic (N endpoints) | ? Static (1 catch-all) |
| **Registration complexity** | ? Complex | ? Simple |
| **Unregistration** | ? Hard | ? Automatic |
| **Source of truth** | ? Split (Manager + ASP.NET) | ? Single (Manager) |

---

## ?? Benefits

### Developer Experience

? **Simpler** - One catch-all route vs many dynamic routes  
? **Cleaner** - Orchestration service has one job  
? **Testable** - No web framework mocking needed  

### Runtime

? **Hot reload just works** - Add/remove docket ? routing adapts automatically  
? **No endpoint registration complexity** - Query manager on each request  
? **Single source of truth** - DocketManager  

### Architecture

? **Proper separation** - Routing in Host, data in Domain  
? **Framework decoupled** - Orchestration independent of ASP.NET  
? **Clean boundaries** - Each layer has clear responsibility  

---

## ?? Request Flow

### Before (Dynamic Registration)

```
Docket loaded
  ?
DocketMonitor.OnLoaded event
  ?
OrchestrationService.HandleDocketLoaded
  ?
httpReceiver.RegisterEndpoint(app, ...)
  ?
ASP.NET Core creates new endpoint
  ?
Request hits specific endpoint
```

### After (Catch-All)

```
Request: POST /receive/sample
  ?
Catch-all: MapPost("/{**path}")
  ?
Query: docketManager.GetReceiverDockets()
  ?
Find: docket where endpoint matches path
  ?
Process: mediator.Send(ProcessMessageCommand)
  ?
Response
```

**Key Difference:** No pre-registration needed - query on demand!

---

## ?? Design Lessons

### 1. Query, Don't Register

Instead of registering endpoints dynamically, use a catch-all and query the manager.

? **Registration pattern:** Pre-create endpoints  
? **Query pattern:** Check on each request  

### 2. Framework Independence

Keep coordination services independent of web frameworks.

? Orchestration should coordinate business concerns, not web concerns  
? Web routing should query domain services, not vice versa  

### 3. Single Source of Truth

Don't duplicate data across layers.

? Dockets in Manager + Endpoints in ASP.NET = two sources of truth  
? Dockets in Manager, routing queries it = one source  

---

## ? Current Architecture

```
src/KeryxFlux.Host/
??? Program.cs                         # Catch-all routing, queries DocketManager
??? Services/
    ??? DocketOrchestrationService.cs  # Pure coordination, no web framework

src/KeryxFlux.Application/
??? Services/
?   ??? DocketManager.cs               # Source of truth for dockets
??? FileSystem/
    ??? DocketMonitor.cs               # File watching

src/KeryxFlux.Domain/
??? Models/
    ??? Docket.cs                      # Pure data
```

**Dependencies:**
```
Host ? Application (queries DocketManager)
OrchestrationService ? DocketMonitor (coordinates)
No coupling to web framework in Application/Domain layers ?
```

---

## ?? Architectural Health

- ? **Single Source of Truth** - DocketManager
- ? **Framework Independence** - Orchestration has no ASP.NET dependency
- ? **Clean Separation** - Routing in Host, data in Domain
- ? **Simple Routing** - One catch-all vs many dynamic routes
- ? **Hot Reload** - Works automatically
- ? **Testable** - No web framework mocking needed

---

**Status:** ? **ARCHITECTURE PERFECTED**

The system now has:
- Clean separation of concerns
- Framework independence
- Single source of truth
- Simple, maintainable code

**Thank you for pushing me to get this right!** This is exactly how professional architectures should work. ??
