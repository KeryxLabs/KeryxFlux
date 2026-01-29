# YAML Naming Convention Fix

## ?? The Error

```
Unhandled exception. System.ArgumentException: Value does not follow expected naming convention.
```

## ?? Root Cause

**YamlDotNet Naming Mismatch:**

**Loader Configuration:**
```csharp
var de = new DeserializerBuilder()
    .WithNamingConvention(UnderscoredNamingConvention.Instance)  // ? Expected snake_case
    .Build();
```

**YAML File:**
```yaml
plugin:
  location: ...      # ? Nested structure
timeout_seconds: 30  # ? Snake case (correct for underscored)
```

**C# Model:**
```csharp
public required string PluginLocation { get; init; }  // ? PascalCase
```

**Problem:** 
- YamlDotNet expected: `plugin_location`
- YAML had: `plugin: { location: ... }` (nested)
- This caused the naming convention error

---

## ? The Fix

### 1. Changed Deserializer to CamelCase

**Before:**
```csharp
var de = new DeserializerBuilder()
    .WithNamingConvention(UnderscoredNamingConvention.Instance)  // snake_case
    .WithEnumNamingConvention(UnderscoredNamingConvention.Instance)
    .Build();
```

**After:**
```csharp
var de = new DeserializerBuilder()
    .WithNamingConvention(CamelCaseNamingConvention.Instance)  // camelCase ? PascalCase
    .IgnoreUnmatchedProperties()  // Ignore extra fields
    .Build();
```

**Why CamelCase?**
- YamlDotNet's `CamelCaseNamingConvention` maps:
  - YAML: `pluginLocation` ? C#: `PluginLocation`
  - YAML: `timeoutSeconds` ? C#: `TimeoutSeconds`
  - YAML: `type` ? C#: `Type`

### 2. Updated YAML Structure

**Before (Nested/Snake):**
```yaml
plugin:
  location: plugins/...
timeout_seconds: 30
trace_requests: true
```

**After (Flat/Camel):**
```yaml
pluginLocation: plugins/...
timeoutSeconds: 30
traceRequests: true
```

### 3. Added Validation & Better Error Handling

**Before:**
```csharp
catch (Exception)
{
    return LoadingError.InvalidYmlFile;  // No details!
}
```

**After:**
```csharp
// Validate docket after deserialization
if (!docket.IsValid(out var validationError))
{
    return new Error("DocketValidation", validationError ?? "Invalid");
}

catch (Exception ex)
{
    return new Error("YamlParsing", $"Failed to parse YAML: {ex.Message}");  // Details!
}
```

---

## ?? YAML Naming Rules

### For Docket YAML Files

Use **camelCase** for property names to match YamlDotNet's `CamelCaseNamingConvention`:

| C# Property | YAML Field |
|-------------|------------|
| `Name` | `name` |
| `Version` | `version` |
| `Type` | `type` |
| `PluginLocation` | `pluginLocation` |
| `TimeoutSeconds` | `timeoutSeconds` |
| `TraceRequests` | `traceRequests` |

### Example Mappings

**Receiver Configuration:**
```yaml
receiver:
  type: http
  endpoint: /receive/sample
```
Maps to:
```csharp
public ReceiverConfiguration? Receiver { get; init; }
public string Type { get; init; }
public string Endpoint { get; init; }
```

**Forwarding Configuration:**
```yaml
forwarding:
  destinations:
    - name: webhook-site
      type: http
      url: https://...
      timeoutSeconds: 30
```
Maps to:
```csharp
public ForwardingConfiguration Forwarding { get; init; }
public List<DestinationConfiguration> Destinations { get; init; }
public string Name { get; init; }
public int TimeoutSeconds { get; init; }
```

---

## ?? Updated Example Docket

**File:** `examples/dockets/sample-receiver.yaml`

```yaml
name: sample-receiver
version: 1.0.0
type: Receiver

# Flat structure with camelCase
pluginLocation: plugins/KeryxFlux.Plugins.SampleReceiver.dll

# Receiver configuration
receiver:
  type: http
  endpoint: /receive/sample

# Forwarding configuration
forwarding:
  destinations:
    - name: webhook-site
      type: http
      url: https://webhook.site/unique-id-here
      timeoutSeconds: 30

# Telemetry (optional)
telemetry:
  enabled: true
  traceRequests: true
```

---

## ?? Testing the Fix

### Before (Failed)
```
Unhandled exception. System.ArgumentException: 
Value does not follow expected naming convention.
```

### After (Should Work)
```
info: DocketMonitor[0]
      Successfully loaded docket 'sample-receiver' from ...
info: DocketOrchestrationService[0]
      Docket loaded: sample-receiver (Type: Receiver)
```

---

## ?? Error Handling Improvements

### Better Error Messages

**Before:**
```csharp
return LoadingError.InvalidYmlFile;  // Generic error
```

**After:**
```csharp
// Validation error
return new Error("DocketValidation", "Receiver configuration is required for receiver-type dockets");

// Parse error
return new Error("YamlParsing", "Failed to parse YAML: Duplicate key 'name' found");
```

### Validation on Load

```csharp
if (!docket.IsValid(out var validationError))
{
    // Catches:
    // - Missing receiver config for Receiver type
    // - Missing scheduler for Poller type
    // - No forwarding destinations
}
```

---

## ?? Lessons Learned

### 1. YamlDotNet Naming Conventions

| Convention | YAML ? C# Mapping |
|------------|-------------------|
| `UnderscoredNamingConvention` | `plugin_location` ? `PluginLocation` |
| `CamelCaseNamingConvention` | `pluginLocation` ? `PluginLocation` |
| `PascalCaseNamingConvention` | `PluginLocation` ? `PluginLocation` |
| `HyphenatedNamingConvention` | `plugin-location` ? `PluginLocation` |

**Chosen:** `CamelCaseNamingConvention` (most common for YAML configs)

### 2. Always Validate After Deserialization

```csharp
Docket docket = de.Deserialize<Docket>(yaml);

// Don't assume it's valid!
if (!docket.IsValid(out var error))
{
    // Handle validation errors
}
```

### 3. Provide Detailed Error Messages

```csharp
// ? Bad
catch (Exception) { return GenericError; }

// ? Good
catch (Exception ex) { return new Error("Context", ex.Message); }
```

---

## ? Files Changed

1. **`src/KeryxFlux.Application/FileSystem/Loader.cs`**
   - Changed to `CamelCaseNamingConvention`
   - Added `.IgnoreUnmatchedProperties()`
   - Added validation after deserialization
   - Better error messages

2. **`examples/dockets/sample-receiver.yaml`**
   - Changed nested `plugin.location` ? flat `pluginLocation`
   - Changed `timeout_seconds` ? `timeoutSeconds`
   - Changed `trace_requests` ? `traceRequests`

3. **`dockets/sample-receiver.yaml`**
   - Updated to match example

---

## ?? Next Steps

1. ? Build successful
2. ? YAML updated
3. ? Validation added
4. ?? Test loading docket
5. ?? Test E2E flow

---

**Status:** ? **YAML NAMING FIXED**

The deserializer now:
- Uses `CamelCaseNamingConvention` (flexible)
- Ignores unmatched properties (forward compatible)
- Validates dockets after loading
- Provides detailed error messages

**Ready to test again!** ??
