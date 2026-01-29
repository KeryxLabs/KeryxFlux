# Architectural Refinement: Proper Service Layering

## ?? Problems Identified

**User caught two critical design violations:**

1. ? **FileSystemWatcher Injection** - DocketMonitor is THE monitor, why inject watchers?
2. ? **Business Logic in Program.cs** - Event subscription and endpoint registration

## ? What We Fixed

### 1. DocketMonitor Owns Its Watchers

**Before (Wrong):**
```csharp
// Program.cs
var ymlWatcher = new FileSystemWatcher(docketsPath, "*.yml");
var yamlWatcher = new FileSystemWatcher(docketsPath, "*.yaml");
return new DocketMonitor(logger, docketManager, docketsPath, ymlWatcher, yamlWatcher);
```

**After (Correct):**
```csharp
// DocketMonitor.cs
public DocketMonitor(ILogger<DocketMonitor> logger, IDocketManager manager, string rootDir)
{
    // DocketMonitor creates and owns its FileSystemWatchers
    _ymlWatcher = new FileSystemWatcher(_rootDir, _ymlFilter);
    _yamlWatcher = new FileSystemWatcher(_rootDir, _yamlFilter);
}
```

**Why:** DocketMonitor IS the file monitor. It should create and manage its own watchers.

---

### 2. DocketOrchestrationService Handles Coordination

**Before (Wrong):**
```csharp
// Program.cs - 50+ lines of business logic
docketMonitor.OnLoaded += (sender, e) =>
{
    app.Logger.LogInformation("Docket loaded: {DocketName}", e.Docket.Name);
    
    if (e.Docket.Type == DocketType.Receiver && e.Docket.Receiver?.Type == "http")
    {
        var endpoint = e.Docket.Receiver.Endpoint ?? $"/receive/{e.Docket.Name}";
        httpReceiver?.RegisterEndpoint(app, e.Docket.Name, endpoint);
        app.Logger.LogInformation("Registered HTTP endpoint...");
    }
};

docketMonitor.OnUnloaded += (sender, e) => { /* ... */ };
docketMonitor.OnReloaded += (sender, e) => { /* ... */ };
docketMonitor.OnError += (sender, e) => { /* ... */ };

docketMonitor.Start();
```

**After (Correct):**
```csharp
// Program.cs - Clean DI registration
builder.Services.AddSingleton<DocketOrchestrationService>();

var app = builder.Build();

// Initialize orchestration (one line!)
var orchestration = app.Services.GetRequiredService<DocketOrchestrationService>();
orchestration.Initialize(app);
```

```csharp
// DocketOrchestrationService.cs - All coordination logic
public class DocketOrchestrationService
{
    public void Initialize(WebApplication app)
    {
        _docketMonitor.OnLoaded += (sender, e) => HandleDocketLoaded(app, e);
        _docketMonitor.OnUnloaded += (sender, e) => HandleDocketUnloaded(e);
        _docketMonitor.OnReloaded += (sender, e) => HandleDocketReloaded(e);
        _docketMonitor.OnError += (sender, e) => HandleDocketError(e);
        
        _docketMonitor.Start();
    }

    private void HandleDocketLoaded(WebApplication app, MonitorInfoEventArgs e)
    {
        // Endpoint registration logic
        if (e.Docket.Type == DocketType.Receiver && e.Docket.Receiver?.Type == "http")
        {
            RegisterHttpEndpoint(app, e.Docket);
        }
    }

    private void RegisterHttpEndpoint(WebApplication app, Docket docket)
    {
        // Implementation details...
    }
}
```

**Why:** Program.cs should only configure and wire dependencies, not contain business logic.

---

## ?? Architectural Principles Applied

### 1. Single Responsibility

? **DocketMonitor** - Watches files, raises events  
? **DocketOrchestrationService** - Coordinates docket lifecycle and endpoint registration  
? **Program.cs** - Configures DI and starts the app  

### 2. Proper Layering

```
src/KeryxFlux.Host/
??? Program.cs                           # ? DI configuration only
??? Services/
    ??? DocketOrchestrationService.cs    # ? Coordination logic

src/KeryxFlux.Application/
??? FileSystem/
    ??? DocketMonitor.cs                 # ? File watching
```

**Why DocketOrchestrationService is in Host, not Application:**
- It references `WebApplication` (ASP.NET Core)
- It references `HttpReceiver` (Infrastructure)
- Application layer shouldn't know about these

### 3. Dependency Rule

? Dependencies point inward:
```
Host ? Infrastructure ? Application ? Domain
```

? Application can't depend on Infrastructure  
? Host can depend on everything (it's the composition root)  

---

## ?? Comparison

### Program.cs

| Metric | Before | After |
|--------|--------|-------|
| **Lines of code** | ~120 | ~80 |
| **Business logic** | ? Yes (50+ lines) | ? No |
| **Event handlers** | ? In Program.cs | ? In service |
| **Testability** | ? Hard | ? Easy |
| **Maintainability** | ? Complex | ? Simple |

### DocketMonitor

| Aspect | Before | After |
|--------|--------|-------|
| **FileSystemWatcher creation** | ? External | ? Internal |
| **Responsibilities** | ? Watch files | ? Watch files |
| **Dependencies injected** | ? 5 params | ? 3 params |

### DocketOrchestrationService

| Aspect | New Service |
|--------|-------------|
| **Location** | Host/Services (correct layer) |
| **Responsibilities** | Coordinate docket lifecycle |
| **Testable** | ? Yes (inject mocks) |
| **Single Responsibility** | ? Yes |

---

## ?? Lessons Reinforced

### 1. The Monitor Creates Its Tools

If a class IS something (like a file monitor), it should create and manage what it needs (like FileSystemWatchers).

**Don't inject implementation details** when the class IS the thing doing the work.

### 2. Keep Host Clean

Program.cs is the **composition root**, not the **implementation location**.

? **DO:** Register services, configure DI  
? **DON'T:** Write business logic, event handlers

### 3. Use Services for Orchestration

When you have coordination logic (subscribing to events, wiring things together), create a dedicated service.

? **DocketOrchestrationService** - Coordinates docket lifecycle  
? Lives in Host because it references ASP.NET Core and Infrastructure  
? Testable by injecting mock dependencies  

### 4. Respect Layer Boundaries

```
Application layer can't reference:
  ? Infrastructure
  ? ASP.NET Core
  ? UI frameworks

Host layer can reference:
  ? Everything (it's the composition root)
```

---

## ? Current Architecture Health

- ? **Program.cs** - Clean, just DI and startup
- ? **DocketMonitor** - Owns its watchers
- ? **DocketOrchestrationService** - Handles coordination
- ? **Layer boundaries** - Respected
- ? **Single Responsibility** - Each class has one job
- ? **Testability** - All dependencies injected

---

## ?? Benefits

### Developer Experience

? **Easy to test** - Mock IDocketMonitor in orchestration service  
? **Easy to understand** - Each class has clear responsibility  
? **Easy to extend** - Add new receivers/pollers in orchestration service  

### Maintainability

? **Program.cs stays simple** - No bloat over time  
? **Coordination logic in one place** - DocketOrchestrationService  
? **File watching isolated** - DocketMonitor  

### Production

? **Clear separation** - Infrastructure vs coordination  
? **Proper layering** - Dependencies point inward  
? **Single Responsibility** - Each component focused  

---

**Status:** ? **ARCHITECTURE REFINED**

Thank you for catching these violations! This is exactly the kind of discipline that keeps projects from becoming spaghetti code.

**Key Takeaway:** When you find yourself writing business logic in Program.cs, create a service. When you're injecting tools that a class should create itself, let it own them.

**The architecture is now cleaner, more maintainable, and properly layered.** ??
