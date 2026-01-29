# KeryxHealth - Product Ecosystem

## Brand Overview

**KeryxHealth** is a suite of interoperability and integration tools for healthcare and enterprise systems.

### Etymology

**Keryx** (Greek: ?????)
- Meaning: Herald, messenger, announcer
- Historical: A person who carried messages between parties in ancient Greece
- Healthcare Context: Perfect metaphor for systems that transmit data between applications

---

## Product Portfolio

### KeryxPars
**Interface Message Parsing Library**

- **Keryx**: Herald/Messenger
- **Pars**: Parts (Latin)
- **Purpose**: Parsing and breaking down interface messages into component parts
- **Repository**: `https://github.com/theelevators/KeryxPars`
- **Type**: Library/SDK

**Use Case**: Parse HL7, FHIR, X12, and other healthcare message formats

---

### KeryxFlux
**Bi-Directional Interoperability Engine**

- **Keryx**: Herald/Messenger
- **Flux**: Flow (Latin)
- **Purpose**: Orchestrating the flow of data between systems
- **Repository**: `https://github.com/theelevators/KeryxFlux`
- **Type**: Service/Platform

**Use Case**: 
- Receive data from HTTP, TCP, RabbitMQ, Kafka
- Transform using plugins (leverage KeryxPars for parsing)
- Forward to destination systems
- Schedule polling jobs

---

## Brand Identity

### Visual Theme
- **Primary Color**: Healthcare blue/teal (trust, reliability)
- **Secondary Color**: Amber/gold (herald, messenger)
- **Logo Concept**: Stylized herald's staff (kerykeion) with data flow arrows

### Naming Convention

All products follow the pattern: `Keryx[Function]`

**Examples:**
- `KeryxPars` - Parsing
- `KeryxFlux` - Flow orchestration
- `KeryxSync` - (Future) Synchronization tool
- `KeryxRoute` - (Future) Routing engine
- `KeryxGuard` - (Future) Security/validation

### .NET Namespace Convention

```csharp
namespace KeryxHealth.Pars.HL7;
namespace KeryxHealth.Flux.Domain;
namespace KeryxHealth.Flux.Infrastructure;
```

Alternative (product-focused):
```csharp
namespace KeryxPars.HL7;
namespace KeryxFlux.Domain;
namespace KeryxFlux.Infrastructure;
```

**Recommendation**: Use product-focused (second option) for clarity

---

## Target Market

### Primary
- Healthcare organizations (hospitals, clinics, HIEs)
- Healthcare IT vendors
- System integrators
- Interoperability engineers

### Secondary
- Enterprise integration teams
- Financial services (similar integration challenges)
- IoT/Manufacturing (data flow patterns)

---

## Competitive Positioning

| Feature | KeryxFlux | Mirth Connect | Rhapsody | Azure Logic Apps |
|---------|-----------|---------------|----------|------------------|
| **Cloud-Native** | ? | ? | ? | ? |
| **DevOps-First** | ? YAML | ? GUI | ? GUI | ?? JSON/Portal |
| **Bi-Directional** | ? | ? | ? | ? |
| **Horizontal Scaling** | ? K8s-ready | ?? Limited | ?? Limited | ? Auto |
| **Plugin System** | ? .NET | ? Java | ? | ?? Connectors |
| **Open Source** | ? | ? | ? | ? |
| **Modern Stack** | ? .NET 10 | ? Java 8+ | ? | ? Cloud |
| **Self-Hosted** | ? | ? | ? | ? |

**Key Differentiator**: Only cloud-native, DevOps-first, YAML-configured interop engine

---

## Marketing Taglines

### KeryxHealth
- "The Herald of Modern Healthcare Integration"
- "Message, Transform, Deliver - The Keryx Way"
- "Healthcare Interoperability, Reimagined"

### KeryxPars
- "Parse Anything, Understand Everything"
- "Breaking Down Barriers in Healthcare Data"

### KeryxFlux
- "Data Flow, Reimagined"
- "Orchestrate Your Integration, DevOps Style"
- "From Code to Cloud in Minutes, Not Months"

---

## Documentation Standards

### Repository README Pattern

```markdown
# [ProductName]

> Part of the [KeryxHealth](https://github.com/theelevators) ecosystem

[Product tagline]

## What is [ProductName]?

[Description]

## Related Projects

- **KeryxPars**: Message parsing library
- **KeryxFlux**: Interoperability engine

## License

Apache 2.0 - see [LICENSE](LICENSE)
```

### Code Header Pattern

```csharp
// -----------------------------------------------------------------------
// <copyright file="FileName.cs" company="KeryxHealth">
//     Copyright (c) 2024 KeryxHealth. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace KeryxFlux.Domain;
```

---

## Website Structure (Future)

### keryxhealth.io (or .com)

```
/
??? /products
?   ??? /pars (KeryxPars documentation)
?   ??? /flux (KeryxFlux documentation)
??? /docs
?   ??? /getting-started
?   ??? /tutorials
?   ??? /api-reference
??? /community
?   ??? /forums
?   ??? /showcase
??? /about
    ??? /company
    ??? /careers
```

---

## Social Media

### GitHub Organization
- **Name**: KeryxHealth
- **URL**: `https://github.com/keryxhealth` (recommended)
- **Current**: `https://github.com/theelevators` (migrate or keep both)

### Twitter/X
- **Handle**: @KeryxHealth
- **Bio**: "Open-source healthcare interoperability tools. Herald of modern integration. ???"

### LinkedIn
- **Company Page**: KeryxHealth
- **Tagline**: "Modern Healthcare Interoperability | Open Source Integration Platform"

---

## Community Building

### Discord Server
- **Name**: KeryxHealth Community
- **Channels**:
  - #general
  - #keryxpars-support
  - #keryxflux-support
  - #plugin-development
  - #showcase
  - #feature-requests

### Reddit
- **Subreddit**: r/KeryxHealth (if needed)

### Stack Overflow
- **Tag**: `keryxflux`, `keryxpars`

---

## Open Source Strategy

### License
- **Recommendation**: Apache 2.0
- **Rationale**: 
  - Patent protection
  - Enterprise-friendly
  - Allows commercial use
  - Encourages contributions

### Contribution Guidelines
- Code of Conduct (Contributor Covenant)
- Contributing.md in each repo
- Issue templates
- PR templates
- Clear roadmap

### Governance
- **Benevolent Dictator** (initial)
- **Steering Committee** (if community grows)
- **RFC Process** for major features

---

## Revenue Model (If Applicable)

### Open Core
- **Free**: Self-hosted KeryxFlux & KeryxPars
- **Paid**: 
  - KeryxFlux Cloud (SaaS)
  - Enterprise support contracts
  - Custom plugin development
  - Training & certification

### Consulting
- Integration services
- Custom implementations
- Performance optimization

### Sponsorship
- GitHub Sponsors
- OpenCollective
- Corporate sponsorships

---

## Next Steps

1. ? **Finalize branding** (KeryxFlux name approved)
2. ?? **Update all documentation** (in progress)
3. ?? **Create GitHub organization** (keryxhealth)
4. ?? **Design logo** (hire designer or community contest)
5. ?? **Reserve domains** (keryxhealth.io, .com)
6. ?? **Setup social media** (Twitter, LinkedIn)
7. ?? **Create website** (GitHub Pages or dedicated)
8. ?? **Launch announcement** (blog post, social media)

---

## Brand Assets Checklist

- [ ] Logo (SVG, PNG in various sizes)
- [ ] Color palette (primary, secondary, accents)
- [ ] Typography guidelines
- [ ] Icon set
- [ ] README badges (build status, version, license)
- [ ] Social media banners
- [ ] Presentation templates
- [ ] Sticker designs (for conferences)

---

**Last Updated**: 2024
**Status**: Branding approved, implementation in progress
