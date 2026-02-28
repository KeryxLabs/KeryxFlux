# Date Variables Quick Reference

## Basic Syntax

```yaml
date_variables:
  - name: variable_name
    offset_expression: "-1h"                    # Required: time offset
    format: "yyyy-MM-dd'T'HH:mm:ss'Z'"         # Optional: date format
    timezone: "UTC"                             # Optional: time zone
    description: "Optional description"         # Optional: documentation
```

## Offset Expressions

| Expression | Meaning | Example Result (from 2025-01-15 15:00) |
|------------|---------|----------------------------------------|
| `0` | Current time | 2025-01-15 15:00:00 |
| `-15m` | 15 minutes ago | 2025-01-15 14:45:00 |
| `-1h` | 1 hour ago | 2025-01-15 14:00:00 |
| `-6h` | 6 hours ago | 2025-01-15 09:00:00 |
| `-1d` | 1 day ago | 2025-01-14 15:00:00 |
| `-7d` | 1 week ago | 2025-01-08 15:00:00 |
| `-30d` | 30 days ago | 2024-12-16 15:00:00 |
| `-1M` | ~1 month ago | 2024-12-16 15:00:00 |
| `+2h` | 2 hours from now | 2025-01-15 17:00:00 |

## Common Date Formats

### API Standards

```yaml
# ISO 8601 (most common)
format: "yyyy-MM-dd'T'HH:mm:ss'Z'"
# Output: 2025-01-15T14:30:00Z

# ISO 8601 with milliseconds
format: "yyyy-MM-dd'T'HH:mm:ss.fff'Z'"
# Output: 2025-01-15T14:30:00.123Z

# Date only
format: "yyyy-MM-dd"
# Output: 2025-01-15

# Compact format
format: "yyyyMMdd"
# Output: 20250115

# US format
format: "MM/dd/yyyy"
# Output: 01/15/2025
```

### Database Formats

```yaml
# SQL Server / PostgreSQL
format: "yyyy-MM-dd HH:mm:ss"
# Output: 2025-01-15 14:30:00

# MySQL datetime
format: "yyyy-MM-dd HH:mm:ss"
# Output: 2025-01-15 14:30:00
```

### Unix Timestamps

```yaml
# Unix timestamp (seconds)
format: "unix"
# Output: 1736948400

# Unix timestamp (milliseconds)
format: "unix_ms"
# Output: 1736948400000
```

## Common Scenarios

### 1-Hour Lookback Window

```yaml
date_variables:
  - name: lookback_date
    offset_expression: "-1h"
    format: "yyyy-MM-dd'T'HH:mm:ss'Z'"
```

**Use in URL**: `?updated_after={lookback_date}`

---

### Time Range (Last 24 Hours)

```yaml
date_variables:
  - name: start_date
    offset_expression: "-24h"
    format: "yyyy-MM-dd"
  - name: end_date
    offset_expression: "0"
    format: "yyyy-MM-dd"
```

**Use in URL**: `?from={start_date}&to={end_date}`

---

### 30-Day Backfill

```yaml
date_variables:
  - name: backfill_start
    offset_expression: "-30d"
    format: "yyyy-MM-dd'T'HH:mm:ss'Z'"
  - name: backfill_end
    offset_expression: "0"
    format: "yyyy-MM-dd'T'HH:mm:ss'Z'"
```

**Use in URL**: `?start={backfill_start}&end={backfill_end}`

---

### Different Formats for Same Time

```yaml
date_variables:
  - name: query_date_iso
    offset_expression: "-1d"
    format: "yyyy-MM-dd'T'HH:mm:ss'Z'"
    
  - name: query_date_compact
    offset_expression: "-1d"
    format: "yyyyMMdd"
```

**Use in URL**: `?iso={query_date_iso}&compact={query_date_compact}`

## Configuration Levels

### Global (Docket Level)

```yaml
name: my-docket
version: 1.0.0

date_variables:
  - name: lookback_date
    offset_expression: "-1h"
```

All tenants and endpoints inherit this configuration.

---

### Tenant Override

```yaml
tenants:
  - tenant_id: ORG_MAIN
    # Uses global date variables
    
  - tenant_id: EDGE_ORG
    # Override for this org only
    date_variables:
      - name: lookback_date
        offset_expression: "-4h"  # Longer lookback
```

---

### Endpoint Override

```yaml
endpoints:
  - endpoint_id: Runs
    # Override for this endpoint only
    date_variables:
      - name: lookback_date
        offset_expression: "-30m"  # Shorter lookback
```

## Time Zones

```yaml
# UTC (default - recommended for most cases)
timezone: "UTC"

# US Eastern Time
timezone: "America/New_York"

# US Pacific Time
timezone: "America/Los_Angeles"

# London
timezone: "Europe/London"

# Tokyo
timezone: "Asia/Tokyo"
```

?? **Best Practice**: Use UTC unless the API specifically requires local time.

## Complete Example

```yaml
name: ci-updates-with-lookback
version: 1.0.0
type: poller
plugin_location: ./plugins/Parser.dll

# Static variables
configuration:
  environment: prod
  api_version: v2

# Date variables
date_variables:
  - name: since_timestamp
    offset_expression: "-2h"
    format: "unix"
    description: "2-hour lookback as Unix timestamp"

scheduler:
  cron_expression: "0 * * * *"  # Every hour
  queue: default
  server:
    name: ci-api
    # Combine static and date variables
    address: "https://api.ci.example/{environment}/{api_version}/runs?since={since_timestamp}"
    authentication:
      type: oauth2
      token_url: https://api.ci.example/oauth/token
      client_id_env: CLIENT_ID
      client_secret_env: CLIENT_SECRET

# Pagination variables (handled by strategy)
pagination:
  strategy: "offset-limit"
  page_size: 100
  # Adds: &offset=0&limit=100, &offset=100&limit=100, etc.

forwarding:
  destinations:
    - name: warehouse
      type: http
      url: https://warehouse.example.com/api/ingest
      method: POST
```

**Resolved URL** (at 2025-01-15 15:00 UTC):
```
Page 1: https://api.ci.example/prod/v2/runs?since=1736942400&offset=0&limit=100
Page 2: https://api.ci.example/prod/v2/runs?since=1736942400&offset=100&limit=100
Page 3: https://api.ci.example/prod/v2/runs?since=1736942400&offset=200&limit=100
```

## Variable Resolution Order

KeryxFlux resolves variables in this order:

1. **Date variables** - `{lookback_date}`, `{start_date}`, `{end_date}`
2. **Static variables** - `{environment}`, `{tenant_id}`, `{api_version}`
3. **Pagination variables** - `{offset}`, `{limit}`, `{cursor}`, `{page}` (handled by strategy)

All three types work together seamlessly!

### Path-Based Pagination

For non-standard APIs with pagination in the URL path:

```yaml
configuration:
  run_id: "12345"
  page_size: "50"

date_variables:
  - name: lookback_date
    offset_expression: "-1h"
    format: "yyyy-MM-dd"

scheduler:
  server:
    # Pagination in path: /{page}/{size}
    address: "https://api.ci.example/Jobs/{run_id}/{page}/{page_size}?since={lookback_date}"

pagination:
  strategy: "page-based"
  page_size: 50
  options:
    page_param: "page"
    page_in_path: true  # Key: tells strategy page is in path
    start_page: 1
```

**Result**:
```
Page 1: https://api.ci.example/Jobs/12345/1/50?since=2025-01-15
Page 2: https://api.ci.example/Jobs/12345/2/50?since=2025-01-15
Page 3: https://api.ci.example/Jobs/12345/3/50?since=2025-01-15
```

? **Works with both query parameters AND path-based pagination!**

## Troubleshooting

### Variable Not Resolving

**Problem**: URL still contains `{variable_name}`

**Solution**: Check variable name matches exactly (case-insensitive)
```yaml
# YAML config
date_variables:
  - name: lookback_date  # ? Correct

# URL
address: "...?date={lookback_date}"  # ? Works
address: "...?date={LookBackDate}"   # ? Also works (case-insensitive)
address: "...?date={lookup_date}"    # ? Typo, won't resolve
```

### Wrong Date/Time

**Problem**: Resolved date is incorrect

**Solutions**:
1. Check offset expression: `-1h` vs `-1d`
2. Verify timezone is correct
3. Enable logging to see resolved values

### Format Error

**Problem**: API rejects date format

**Solution**: Match API's expected format exactly
```yaml
# API expects: 2025-01-15
format: "yyyy-MM-dd"  # ? Correct

# API expects: 01/15/2025
format: "MM/dd/yyyy"  # ? Correct

# API expects Unix timestamp
format: "unix"  # ? Correct
```

## Validation

The system validates:
- ? Offset expression syntax
- ? Time zone exists
- ? Format string is valid

Invalid configurations will be logged at startup.

## See Full Documentation

- **Comprehensive Guide**: [docs/DATE_VARIABLES.md](../docs/DATE_VARIABLES.md)
- **Examples**: [dockets/examples/date-variables/](./examples/date-variables/)
- **README**: [README.md](../README.md)
