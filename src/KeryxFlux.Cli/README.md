# KeryxFlux CLI

Command-line tool for managing KeryxFlux docket files.

## Installation

### Install from NuGet (Coming Soon)

```bash
# Install globally
dotnet tool install -g KeryxFlux.Cli

# Verify installation
keryxflux --version
```

### Install from Source

```bash
# Clone repository
git clone https://github.com/KeryxLabs/KeryxFlux.git
cd KeryxFlux

# Build and pack
dotnet pack src/KeryxFlux.Cli/KeryxFlux.Cli.csproj -c Release

# Install globally from local build
dotnet tool install -g --add-source ./src/KeryxFlux.Cli/bin/Release KeryxFlux.Cli
```

### Update to Latest Version

```bash
# Update from NuGet
dotnet tool update -g KeryxFlux.Cli

# Or update from local build
dotnet tool update -g --add-source ./src/KeryxFlux.Cli/bin/Release KeryxFlux.Cli
```

### Uninstall

```bash
# Uninstall global tool
dotnet tool uninstall -g KeryxFlux.Cli
```

### Check Installed Version

```bash
# Show version
keryxflux --version

# List all installed tools
dotnet tool list -g
```

## Usage

### Validate Dockets

Validate one or more docket files:

```bash
# Validate single file
keryxflux validate product-sync.yaml

# Validate directory
keryxflux validate ./dockets

# Verbose output
keryxflux validate product-sync.yaml -v
```

**Output:**
```
???????????????????????????????????????
? File             ? Status  ? Issues ?
???????????????????????????????????????
? product-sync.yaml? ? Valid ? None   ?
???????????????????????????????????????

Summary: 1/1 valid, 0 invalid, 0 warnings
```

### Preview Date Variables & URLs

Preview how variables will be resolved and what URLs will be generated:

```bash
# Preview with current time
keryxflux preview product-sync.yaml

# Preview at specific time
keryxflux preview product-sync.yaml --at "2025-01-15T15:00:00Z"

# Preview specific tenant only
keryxflux preview product-sync.yaml --tenant STORE001
```

**Output:**
```
Preview for 'product-sync-poller' at 2025-01-15 15:00:00 UTC

Date Variables:
???????????????????????????????????????????????????
? Variable        ? Offset ? Resolved Value       ?
???????????????????????????????????????????????????
? {lookback_date} ? -1h    ? 2025-01-15T14:00:00Z ?
???????????????????????????????????????????????????

Multi-Tenant Expansion:

?? STORE_NYC - New York Store ?????????????????????????????????
? Date Variables (tenant-specific):                           ?
?   {lookback_date} ? 2025-01-15T14:00:00Z                     ?
?                                                              ?
? Endpoints:                                                   ?
?   Products:                                                  ?
?     https://api.shop.com/STORE_NYC/products?updated=gt2025-01-15T14:00:00Z?
????????????????????????????????????????????????????????????????
```

### Create Dockets from Templates

Generate new docket files from built-in templates:

```bash
# Create poller docket
keryxflux create poller -n "patient-sync" -o ./dockets

# Create listener docket
keryxflux create listener -n "event-processor"

# Create webhook docket
keryxflux create webhook -n "order-webhook"
```

**Available Templates:**
- `poller` - HTTP polling with date variables and pagination
- `listener` - Message queue/stream listener
- `webhook` - Webhook receiver

**Output:**
```
? Created docket: ./dockets/patient-sync.yaml

Next steps:
  1. Edit the docket: ./dockets/patient-sync.yaml
  2. Validate: keryxflux validate ./dockets/patient-sync.yaml
  3. Preview: keryxflux preview ./dockets/patient-sync.yaml
```

### Debug Docket Configuration

Deep dive into docket configuration, variable resolution, and multi-tenant expansion:

```bash
# Full debug report
keryxflux debug product-sync.yaml

# Show variable resolution only
keryxflux debug product-sync.yaml --resolve

# Show multi-tenant expansion matrix
keryxflux debug product-sync.yaml --expansion

# Debug at specific time
keryxflux debug product-sync.yaml --at "2025-01-15T15:00:00Z"
```

**Output:**
```
Debug Report for 'test-poller'
Generated at 2026-01-31 01:45:20 UTC

??Basic Information????????
? Name:      test-poller  ?
? Version:   1.0.0        ?
? Type:      Poller       ?
? Plugin:    N/A          ?
? Schedule:  */30 * * * * ?
? Queue:     default      ?
???????????????????????????

Variable Resolution
??? Static Variables
?   ??? {environment} = production
?   ??? {api_base} = https://api.example.com
??? Date Variables (at 2026-01-31 01:45:20 UTC)
?   ??? {lookback_date}
?       ??? Offset: -1h
?       ??? Format: yyyy-MM-dd'T'HH:mm:ss'Z'
?       ??? Calculated: 2026-01-31 00:45:20
?       ??? Result: 2026-01-31T00:45:20Z
??? URL Template
    ??? {{api_base}}/data?since={{lookback_date}}
    ??? Resolved: https://api.example.com/data?since=2026-01-31T00:45:20Z
```

## Commands

### validate

Validate docket file structure and configuration.

**Usage:**
```bash
keryxflux validate <path> [options]
```

**Arguments:**
- `path` - Path to docket file or directory

**Options:**
- `-v, --verbose` - Show detailed validation results

**Validations:**
- YAML syntax
- Required fields (name, version, scheduler)
- Date variable configuration
- Multi-tenant setup
- Duplicate tenant/endpoint IDs

---

### preview

Preview variable resolution and generated URLs without running the docket.

**Usage:**
```bash
keryxflux preview <path> [options]
```

**Arguments:**
- `path` - Path to docket file

**Options:**
- `--at <time>` - Preview at specific time (ISO 8601 format)
- `--tenant <id>` - Preview specific tenant only

**Shows:**
- Resolved date variables
- Generated URLs for each tenant/endpoint
- Merged configurations

---

### create

Create a new docket from a template.

**Usage:**
```bash
keryxflux create <template> [options]
```

**Arguments:**
- `template` - Template type (poller, listener, webhook)

**Options:**
- `-n, --name <name>` - Name of the docket
- `-o, --output <dir>` - Output directory (default: ./dockets)

**Templates:**
- `poller` - Scheduled HTTP polling with date variables
- `listener` - Message queue/stream consumer
- `webhook` - HTTP webhook receiver

---

### debug

Deep debugging of docket configuration and variable resolution.

**Usage:**
```bash
keryxflux debug <path> [options]
```

**Arguments:**
- `path` - Path to docket file

**Options:**
- `--resolve` - Show only variable resolution details
- `--expansion` - Show only multi-tenant expansion matrix
- `--at <time>` - Debug at specific time (ISO 8601 format)

**Shows:**
- Basic docket information
- Static and date variable resolution with calculations
- URL template resolution
- Multi-tenant expansion matrix
- Tenant-specific overrides

---

## Examples

### Validate All Dockets Before Deploy

```bash
keryxflux validate ./dockets -v
if [ $? -eq 0 ]; then
  echo "All dockets valid, deploying..."
  kubectl apply -f ./k8s/dockets.yaml
fi
```

### Preview Before Backfill

```bash
# Check what URLs will be generated for 30-day backfill
keryxflux preview backfill-30days.yaml --at "2025-01-01T00:00:00Z"
```

### Create & Validate New Docket

```bash
# Create new poller
keryxflux create poller -n "observation-sync" -o ./dockets

# Edit the generated file
nano ./dockets/observation-sync.yaml

# Validate
keryxflux validate ./dockets/observation-sync.yaml -v

# Preview with current time
keryxflux preview ./dockets/observation-sync.yaml
```

## Development

### Prerequisites

- .NET 8 SDK or later
- .NET 10 SDK for building the CLI project

### Build

```bash
dotnet build src/KeryxFlux.Cli/KeryxFlux.Cli.csproj
```

### Run Locally (Without Installing)

```bash
# Run commands directly
dotnet run --project src/KeryxFlux.Cli/KeryxFlux.Cli.csproj -- validate test.yaml
dotnet run --project src/KeryxFlux.Cli/KeryxFlux.Cli.csproj -- preview test.yaml
dotnet run --project src/KeryxFlux.Cli/KeryxFlux.Cli.csproj -- debug test.yaml
```

### Pack as Tool

```bash
# Create NuGet package
dotnet pack src/KeryxFlux.Cli/KeryxFlux.Cli.csproj -c Release

# Package will be in:
# src/KeryxFlux.Cli/bin/Release/KeryxFlux.Cli.{version}.nupkg
```

### Local Testing

```bash
# Uninstall existing version (if any)
dotnet tool uninstall -g KeryxFlux.Cli

# Install from local build
dotnet pack src/KeryxFlux.Cli/KeryxFlux.Cli.csproj -c Release
dotnet tool install -g --add-source ./src/KeryxFlux.Cli/bin/Release KeryxFlux.Cli

# Test it
keryxflux --help
```

### Publish to NuGet

```bash
# Pack with version
dotnet pack src/KeryxFlux.Cli/KeryxFlux.Cli.csproj -c Release -p:Version=1.0.0

# Push to NuGet (requires API key)
dotnet nuget push src/KeryxFlux.Cli/bin/Release/KeryxFlux.Cli.1.0.0.nupkg \
  --api-key YOUR_API_KEY \
  --source https://api.nuget.org/v3/index.json
```

## Tech Stack

- **Cocona** - Command routing and argument parsing
- **Spectre.Console** - Rich terminal output
- **YamlDotNet** - YAML parsing

## Roadmap

Future commands:
- `keryxflux test` - Test docket against live endpoint
- `keryxflux migrate` - Schema version migrations
- `keryxflux lint` - Style and best practice checks

---

## Character Encoding

The CLI uses UTF-8 encoding for proper display of special characters (?, ?, etc.). If you see question marks instead, ensure your terminal supports UTF-8:

**Windows PowerShell:**
```powershell
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
```

**Windows Terminal:**
Already uses UTF-8 by default.

**Linux/Mac:**
Already uses UTF-8 by default.

---

## Troubleshooting

### Command Not Found After Installation

If `keryxflux` command is not recognized:

**Windows:**
```powershell
# Check if .NET tools are in PATH
echo $env:PATH

# Tools are usually in:
# %USERPROFILE%\.dotnet\tools

# Add to PATH if missing (PowerShell):
$env:PATH += ";$env:USERPROFILE\.dotnet\tools"
```

**Linux/Mac:**
```bash
# Check if .NET tools are in PATH
echo $PATH

# Tools are usually in:
# ~/.dotnet/tools

# Add to PATH if missing:
export PATH="$PATH:$HOME/.dotnet/tools"

# Make permanent (add to ~/.bashrc or ~/.zshrc):
echo 'export PATH="$PATH:$HOME/.dotnet/tools"' >> ~/.bashrc
```

### Tool Already Installed Error

```bash
# Uninstall first, then reinstall
dotnet tool uninstall -g KeryxFlux.Cli
dotnet tool install -g KeryxFlux.Cli
```

### Version Conflicts

```bash
# Check installed version
dotnet tool list -g | grep -i keryxflux

# Update to latest
dotnet tool update -g KeryxFlux.Cli

# Or uninstall and reinstall
dotnet tool uninstall -g KeryxFlux.Cli
dotnet tool install -g KeryxFlux.Cli
```

### Special Characters Not Displaying

If you see question marks (?) instead of checkmarks (?):

1. Ensure terminal supports UTF-8
2. Use Windows Terminal instead of PowerShell ISE
3. Run: `[Console]::OutputEncoding = [System.Text.Encoding]::UTF8` (PowerShell)

---

## Contributing

We welcome contributions! Please:

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

### Code Style

- Follow existing code patterns
- Use Shouldly for test assertions
- Keep commands focused and single-purpose
- Document new features in this README

---

## License

See LICENSE file in repository root.

---

## Support

- **Issues**: https://github.com/KeryxLabs/KeryxFlux/issues
- **Discussions**: https://github.com/KeryxLabs/KeryxFlux/discussions
- **Documentation**: https://github.com/KeryxLabs/KeryxFlux/docs
