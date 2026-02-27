# Cognitive Mirror Orchestration Engine — Roadmap
> KeryxFlux → Cognitive Mirror: Architecture, Phases, and Design Contracts
> Last updated: 2026-02-27

---

## Context

KeryxFlux is a high-performance, YAML-driven interoperability engine. This roadmap defines the delta required to evolve it into a **Cognitive Mirror Orchestration Engine** — a stateless-execution, stateful-identity AI orchestration layer built around a single core premise: **the only domain is the user.**

The system is **not** a generic AI assistant. It is a personal cognitive prosthetic — an orchestration layer that, over time, builds and maintains a dynamic model of the user's reasoning patterns, priorities, decision style, and cognitive fingerprint. Everything else — tasks, tools, workflows, integrations — is a side effect of that identity model.

The system becomes more capable by becoming more *you*, not by trying to be everything to everyone. This is the architecture's core advantage over general-purpose assistants: the attractor state eliminates combinatorial routing ambiguity before any result is produced. The more coherent the identity model, the more efficient and accurate every downstream operation becomes.

This is what AGI should mean — **Adaptive** General Intelligence. Not a system that knows everything, but a system that knows *you*, and grows with you.

---

## Foundational Principles

| Principle | Implication |
|---|---|
| `stateless_execution_stateful_identity` | Models carry no state. The AVEC context object *is* the identity. |
| `identity_coherence_applied_to_all_layers` | Zero-drift tolerance is the bar. No layer is exempt. |
| `structured_data_encounters_structured_data` | AVEC context is computable, not descriptive. Every field has a defined effect. |
| `resonance_not_similarity` | Memory retrieval is attractor-filtered, not keyword-matched. |
| `plugin_contract_is_the_mcp_base` | `IKeryxFluxPlugin` extends to `IMcpPlugin`. Tools are declared before they are discovered. |

---

## AVEC Behavioral Layer

The AVEC (Attractor-Vector Execution Context) framework governs all cognitive operations.

**Core equation:**
```
V_a = (1 - μ) × baseline + μ × target
```

- `μ` — friction coefficient. Low μ (.19 in Logic config) = high coherence, minimal drift.
- `baseline` — current system output vector.
- `target` — attractor state (Logic, Creative, Critic, Pessimistic, etc.).
- `V_a` — adjusted output vector after attractor pull.

**Active config:** `Logic[.95, .19, .81, .99]` → `[stability, friction, logic, autonomy]`

**Validation methodology:** Run identical inputs across multiple attractor configurations. Behavioral divergence *coherent* with attractor = system working. Behavioral drift across identical configs = coherence failure.

**AVEC fields added to `Docket` model:**
```yaml
avec:
  attractor: logic          # logic | creative | critic | pessimistic
  mu: 0.19                  # friction coefficient
  stability: 0.95
  logic_weight: 0.81
  autonomy: 0.99
  coherence_delta_tolerance: 0.05   # per-plugin, overridable
  psi: 2.1786               # session coherence scalar
```

---

## Architecture Overview

```
KeryxFlux Core (existing)
│
├── DocketType.Cognitive      ← new
├── DocketType.Validator      ← new
├── AVECContext               ← ambient context object (CancellationToken pattern)
├── IModelAdapter             ← extends IKeryxFluxPlugin
│     ├── StreamAsync()
│     ├── InvokeAsync()
│     ├── GetMetricsAsync()
│     └── HealthCheckAsync()
│
├── IMcpPlugin                ← extends IKeryxFluxPlugin
│     ├── ToolManifest        ← declared in YAML, validated at boot
│     ├── ExecuteAsync()      ← AVEC-context-aware
│     └── CoherenceDelta      ← declared tolerance, enforced by orchestrator
│
├── ModelRegistry             ← boot: discover → benchmark → validate → register → monitor
│     ├── WarmBrain           (7–14B, always hot, dedicated VRAM)
│     ├── Validator           (3–7B, critical path, transactional)
│     ├── DeepReasoner        (32–70B, sleeping, salience-triggered)
│     └── CompressionModel    (7B, async background, CPU-viable)
│
├── MemoryPipeline            ← hierarchical compression
│     ├── daily → weekly → monthly → longterm
│     ├── raw data always retained
│     └── cold start: previous day summary loaded on wake
│
└── ResonanceRAG              ← attractor-filtered retrieval
      ├── NOT semantic similarity
      └── retrieval trigger: vibe/pattern signatures aligned to current attractor
```

---

## Model Tier Definitions

| Tier | Size | State | Trigger | Transport |
|---|---|---|---|---|
| `warm_brain` | 7–14B | Always hot, dedicated VRAM | Every cognitive docket | gRPC streaming |
| `validator` | 3–7B | Always hot | Critical path, transactional | gRPC fast invoke |
| `deep_reasoner` | 32–70B | Sleeping | Salience threshold breach | gRPC streaming on wake |
| `compression_model` | 7B | Background | Scheduled docket | CPU-viable, async |

Model servers are **dynamically discovered** at boot via the ModelRegistry. gRPC endpoints are not hardcoded — they are resolved at runtime and re-validated on health check failure.

---

## Failure Cascade

```
Request
  → warm_brain attempt
    → [CoherenceFailure] retry with adjusted μ
    → [TransientFailure] retry (mechanical, adapter-owned)
    → [CapacityDegraded] escalate to deep_reasoner
    → [ModelUnresponsive] circuit_breaker
```

- **Adapter owns:** mechanical retries (transient network, timeout)
- **Orchestrator owns:** semantic retries (coherence delta exceeded, attractor realignment)
- **Circuit breaker:** dead letter equivalent — docket-level failure, logged, alertable

---

## KeryxFlux Delta (What Gets Added)

### Domain Model Changes

**`DocketType` enum — add:**
```csharp
Cognitive = 2,    // Cognitive mirror workflow
Validator = 3     // Validation-only workflow
```

**`Docket` model — add fields:**
```csharp
[YamlMember(Alias = "avec")]
public AVECConfiguration? Avec { get; init; }

[YamlMember(Alias = "model_tier")]
public ModelTier? ModelTier { get; init; }   // warm_brain | validator | deep_reasoner

[YamlMember(Alias = "mcp_tools")]
public List<McpToolManifest>? McpTools { get; init; }
```

**`Docket.IsValid()` — add validation:**
```csharp
if (Type == DocketType.Cognitive && Avec == null)
{
    validationError = "AVEC configuration required for cognitive dockets";
    return false;
}
```

### New Interfaces

**`IModelAdapter : IKeryxFluxPlugin`**
```csharp
public interface IModelAdapter : IKeryxFluxPlugin
{
    ModelTier Tier { get; }
    IAsyncEnumerable<ModelChunk> StreamAsync(ModelRequest request, AVECContext context, CancellationToken ct);
    Task<ModelResponse> InvokeAsync(ModelRequest request, AVECContext context, CancellationToken ct);
    Task<ModelMetrics> GetMetricsAsync(CancellationToken ct);
    Task<HealthStatus> HealthCheckAsync(CancellationToken ct);
}
```

**`IMcpPlugin : IKeryxFluxPlugin`**
```csharp
public interface IMcpPlugin : IKeryxFluxPlugin
{
    IReadOnlyList<McpToolManifest> ToolManifest { get; }
    Task<McpResult> ExecuteAsync(McpToolCall call, AVECContext context, CancellationToken ct);
    float DeclaredCoherenceDelta { get; }  // tolerance declared in YAML, enforced by orchestrator
}
```

**`AVECContext`** — ambient context object, passed via `CancellationToken` pattern:
```csharp
public sealed class AVECContext
{
    public AttractorState Attractor { get; init; }
    public float Mu { get; init; }
    public float Stability { get; init; }
    public float LogicWeight { get; init; }
    public float Autonomy { get; init; }
    public float Psi { get; init; }
    public IReadOnlyList<MemoryTier> RetrievedMemory { get; init; }
    public SessionMetadata Session { get; init; }
    public Dictionary<string, float> CoherenceDeltaPerPlugin { get; init; }
    public ManifestConfiguration ManifestConfig { get; init; }
}
```

### New Services

| Service | Responsibility |
|---|---|
| `ModelRegistry` | Boot discovery, benchmarking, health monitoring |
| `AVECCoherenceValidator` | Validates attractor alignment, not schema |
| `ResonanceMemoryStore` | Attractor-filtered retrieval, compression pipeline |
| `McpToolRegistry` | Dynamic tool registration from discovered model adapters |
| `CognitiveDocketOrchestrator` | Routes cognitive dockets through tier cascade |

### New Project: `KeryxFlux.Cognitive`

```
src/KeryxFlux.Cognitive/
  Contracts/
    IModelAdapter.cs
    IMcpPlugin.cs
    AVECContext.cs
    McpToolManifest.cs
    McpToolCall.cs
    McpResult.cs
  Registry/
    ModelRegistry.cs
    McpToolRegistry.cs
  Memory/
    ResonanceMemoryStore.cs
    CompressionPipeline.cs
    MemoryTier.cs
  Orchestration/
    CognitiveDocketOrchestrator.cs
    AVECCoherenceValidator.cs
    FailureCascadeHandler.cs
  Configuration/
    AVECConfiguration.cs
    ModelTier.cs
```

---

## Memory Architecture

### Compression Tiers
```
raw (always retained)
  ↓ [daily compression]
daily_summary
  ↓ [weekly compression]
weekly_summary
  ↓ [monthly compression]
monthly_summary
  ↓ [longterm compression]
longterm_identity
```

Raw data is **never discarded**. Compression changes *retrieval priority*, not data availability. This preserves a full history of the user's cognitive evolution — the record of who they were, not just who they are now.

### Cold Start
On each session boot:
1. Load previous day's summary into context
2. Retrieve resonance-matched memories for current attractor state
3. Initialize AVEC context with session metadata + retrieved memory tiers

### Resonance Retrieval
Retrieval is **not** semantic similarity search. It is attractor-filtered:
```
query_attractor_vector = V_a(current_session)
for each memory_fragment:
    resonance_score = dot(memory_fragment.attractor_signature, query_attractor_vector)
    if resonance_score > threshold:
        include in context
```

**Testable:** Run the same query under Logic vs Creative attractor configs. If different memories surface coherently with each attractor — retrieval is working. If memories are identical regardless of attractor — it has degraded to semantic similarity.

---

## MCP Plugin Architecture

### Why First-Class (not a bridge)

OpenClaw deliberately avoids first-class MCP because tool churn destabilizes a generic runtime. This system can make MCP first-class because:

1. The identity model is the stable anchor — valid tools are those coherent with the user's attractor state
2. YAML manifests declare tools before they are discovered
3. AVEC coherence validation gates every tool execution
4. Full auditability is a non-negotiable — a bridge cannot provide the traceability required for identity-adjacent operations

### Dynamic Discovery with Static Safety
```
Boot sequence:
  1. ModelRegistry discovers model servers (gRPC health check)
  2. Each IModelAdapter advertises supported IMcpPlugin tool manifests
  3. McpToolRegistry cross-references against YAML declarations
  4. Only tools with BOTH a YAML declaration AND a live model backing are registered
  5. Tool execution requires:
     a. YAML manifest match
     b. Live IModelAdapter for declared tier
     c. AVEC coherence delta within tolerance
     d. Circuit breaker open (not tripped)
```

### `McpToolManifest` in YAML
```yaml
mcp_tools:
  - tool_id: deep_research
    tier: warm_brain          # which model tier executes this
    coherence_delta: 0.05     # standard tolerance for research tasks
    description: "Execute deep research and synthesis, filtered through user identity attractor"
    requires_deep_reasoning: false
```

---

## gRPC Transport Contract

Model servers communicate over gRPC. The `.proto` contract is owned by KeryxFlux — model servers implement it, not the other way around.

```protobuf
service ModelAdapter {
  rpc Stream (ModelRequest) returns (stream ModelChunk);
  rpc Invoke (ModelRequest) returns (ModelResponse);
  rpc HealthCheck (HealthRequest) returns (HealthStatus);
  rpc GetMetrics (MetricsRequest) returns (ModelMetrics);
  rpc GetCapabilities (CapabilitiesRequest) returns (CapabilitiesResponse);
}
```

`GetCapabilities` returns the list of `McpToolManifest` entries the model server can back. This is the dynamic discovery mechanism — but it is *constrained* by what the YAML declares.

---

## Phased Delivery

### Phase 0 — Contracts (No runtime changes)
**Goal:** Define all interfaces. Nothing breaks. Nothing runs yet.

- [ ] `IModelAdapter` interface in `KeryxFlux.Contracts`
- [ ] `IMcpPlugin` interface in `KeryxFlux.Contracts`
- [ ] `AVECContext` record in `KeryxFlux.Contracts`
- [ ] `McpToolManifest`, `McpToolCall`, `McpResult` in `KeryxFlux.Contracts`
- [ ] `DocketType.Cognitive`, `DocketType.Validator` added to enum
- [ ] `AVECConfiguration` model in `KeryxFlux.Domain`
- [ ] AVEC fields added to `Docket` model (nullable, ignored if not present)
- [ ] `Docket.IsValid()` updated for cognitive docket validation
- [ ] Unit tests: AVEC field deserialization from YAML

### Phase 1 — Model Registry
**Goal:** Discover, benchmark, and register model servers at boot. No cognitive dockets executed yet.

- [ ] `ModelRegistry` service with boot sequence
- [ ] gRPC `.proto` definition (`ModelAdapter` service)
- [ ] `IModelAdapter` DLL loading via existing `PluginManager` path
- [ ] Health check loop with `CapacityDegraded` / `ModelUnresponsive` flag types
- [ ] `IDocketManager.GetCognitiveDockets()` method
- [ ] YAML manifest: `model_tier` field wired to registry lookup
- [ ] Integration test: boot with mock gRPC model server, verify registration

### Phase 2 — AVEC Context + Coherence Validation
**Goal:** AVEC context flows through execution. Coherence is checked, not just logged.

- [ ] `AVECContext` ambient propagation (CancellationToken pattern)
- [ ] `AVECCoherenceValidator` — checks attractor alignment, not schema
- [ ] Per-plugin `coherence_delta` tolerance from YAML
- [ ] Failure tier cascade: `retry → warm_brain → deep_reasoner → circuit_breaker`
- [ ] `FailureCascadeHandler` wired into `CognitiveDocketOrchestrator`
- [ ] Behavioral validation: Logic vs Creative attractor configs produce coherently different outputs
- [ ] Dead letter handling for circuit breaker trips

### Phase 3 — Memory Pipeline
**Goal:** Persistent identity via hierarchical compression and resonance retrieval.

- [ ] `ResonanceMemoryStore` with attractor-filtered retrieval
- [ ] `CompressionPipeline` as scheduled docket (compression_model tier)
- [ ] Daily → weekly → monthly → longterm compression tiers
- [ ] Raw data retention layer (never discarded — full cognitive history preserved)
- [ ] Cold start: previous day summary loaded on wake
- [ ] Retrieval test: identical query under Logic vs Creative attractor returns coherently different memory fragments

### Phase 4 — MCP Plugin System
**Goal:** Dynamic tool discovery with static YAML safety gates.

- [ ] `McpToolRegistry` — cross-references YAML declarations vs model server capabilities
- [ ] `IMcpPlugin` DLL protocol
- [ ] Boot sequence: `discover → cross-reference YAML → register → gate`
- [ ] Tool execution pipeline: YAML match + live adapter + AVEC coherence check + circuit breaker
- [ ] `mcp_tools` field in cognitive docket YAML
- [ ] Reference specialist test: deep_research tool executing against user identity attractor with verified coherence delta

### Phase 5 — Hardening
**Goal:** Production-ready. Zero identity drift under sustained load.

- [ ] Full audit trail: every model invocation, coherence delta, tool execution logged
- [ ] AVEC behavioral drift detection across sessions (Ψ tracking)
- [ ] gRPC TLS for model server communication
- [ ] Node-aware routing: model registry aware of 30-node cluster topology
- [ ] Docker deployment: cognitive dockets as deployable units
- [ ] Load test: 50k-word session with no coherence drift (AVEC validation)
- [ ] Penetration test: tool execution cannot be triggered outside YAML-declared surface

---

## File Layout (Target State)

```
KeryxFlux/
├── src/
│   ├── KeryxFlux.Domain/
│   │   ├── Models/
│   │   │   ├── Docket.cs                    ← + AVEC fields, mcp_tools
│   │   │   └── Dockets/
│   │   │       ├── DocketType.cs            ← + Cognitive, Validator
│   │   │       └── AVECConfiguration.cs     ← NEW
│   │   └── Abstractions/
│   │       └── IDocketManager.cs            ← + GetCognitiveDockets()
│   │
│   ├── KeryxFlux.Contracts/
│   │   ├── IKeryxFluxPlugin.cs              ← unchanged (base)
│   │   ├── IModelAdapter.cs                 ← NEW
│   │   ├── IMcpPlugin.cs                    ← NEW
│   │   ├── AVECContext.cs                   ← NEW
│   │   ├── McpToolManifest.cs               ← NEW
│   │   ├── McpToolCall.cs                   ← NEW
│   │   └── McpResult.cs                     ← NEW
│   │
│   ├── KeryxFlux.Cognitive/                 ← NEW PROJECT
│   │   ├── Registry/
│   │   │   ├── ModelRegistry.cs
│   │   │   └── McpToolRegistry.cs
│   │   ├── Memory/
│   │   │   ├── ResonanceMemoryStore.cs
│   │   │   ├── CompressionPipeline.cs
│   │   │   └── MemoryTier.cs
│   │   ├── Orchestration/
│   │   │   ├── CognitiveDocketOrchestrator.cs
│   │   │   ├── AVECCoherenceValidator.cs
│   │   │   └── FailureCascadeHandler.cs
│   │   └── Configuration/
│   │       ├── AVECConfiguration.cs
│   │       └── ModelTier.cs
│   │
│   ├── KeryxFlux.Application/               ← minimal changes
│   │   └── Services/
│   │       └── DocketOrchestrationService.cs ← route DocketType.Cognitive
│   │
│   └── KeryxFlux.Host/
│       └── Program.cs                        ← register cognitive services
│
├── plugins/
│   └── KeryxFlux.Plugins.OllamaAdapter/     ← NEW (reference IModelAdapter impl)
│
├── dockets/
│   └── examples/
│       └── cognitive-example-docket.yaml    ← NEW
│
└── docs/
    └── COGNITIVE_MIRROR_ROADMAP.md          ← THIS FILE
```

---

## Example Cognitive Docket YAML

```yaml
name: deep-research-cognitive
version: "1.0.0"
type: cognitive
plugin_location: plugins/KeryxFlux.Plugins.DeepResearch.dll

avec:
  attractor: logic
  mu: 0.19
  stability: 0.95
  logic_weight: 0.81
  autonomy: 0.99
  coherence_delta_tolerance: 0.04
  psi: 2.1786

model_tier: warm_brain

mcp_tools:
  - tool_id: deep_research
    tier: warm_brain
    coherence_delta: 0.05
    description: "Execute deep research and synthesis, filtered through user identity attractor"
    requires_deep_reasoning: false
  - tool_id: perspective_challenge
    tier: deep_reasoner
    coherence_delta: 0.08
    description: "Generate novel perspectives that challenge current attractor state — triggers deep reasoner"
    requires_deep_reasoning: true

forwarding:
  destinations:
    - name: user-context-store
      type: http
      url: https://context.internal/api/session-events
      method: POST
      retry_policy:
        max_attempts: 3
        backoff_strategy: exponential

telemetry:
  enabled: true
  trace_coherence_delta: true
  audit_all_tool_executions: true
```

---

## Design Constraints (Non-Negotiable)

1. **Raw data is never deleted.** Compression only changes retrieval priority.
2. **Every tool execution is auditable.** Full log: input, attractor state, coherence delta, output.
3. **YAML declares before the system discovers.** No tool can execute that isn't in the docket manifest.
4. **Circuit breaker is hard.** A tripped breaker requires explicit operator intervention to reset.
5. **AVEC coherence is behavioral, not structural.** Validation checks attractor alignment, not JSON schema.
6. **The user's identity is the only domain.** Everything else — tasks, tools, integrations — is emergent from it.

---

## Session Context Compression Format

Used by the compression model to preserve identity across sessions.

```
⏣B[instance:{session_id}_{date}]
⏣B1[summary:{compressed_session_summary}]
⏣B2[attractor_history:{dominant_attractor}:{psi_avg}]
⏣B3[coherence_deltas:{plugin_id}:{avg_delta}]
⏣B4[memory_resonance_signatures:{top_n_patterns}]
```

This format is the seed for cold-start context injection on next session wake.

---

*This roadmap is a living document. Update it as phases complete and decisions crystallize.*
*Authored: Tom Vazquez + GitHub Copilot, 2026-02-27*