# Date Variables Examples

This directory contains example dockets demonstrating date variable functionality for time-based polling and backfill scenarios.

## Overview

Date variables allow you to dynamically generate URLs with calculated dates based on the current time. This is essential for:

- **Lookback windows**: Poll for data updated in the last N hours/days
- **Backfill scenarios**: Retrieve historical data from a specific time range
- **Incremental sync**: Get data since the last successful poll

## Date Variable Configuration

```yaml
date_variables:
  - name: lookback_date          # Variable name: use as {lookback_date} in URLs
    offset_expression: "-1h"     # 1 hour ago
    format: "yyyy-MM-dd'T'HH:mm:ss'Z'"  # ISO 8601 format
    timezone: "UTC"              # Time zone (default: UTC)
    description: "1 hour lookback for delayed updates"
```

### Offset Expression Format

- **Format**: `[+/-][number][unit]`
- **Units**:
  - `s` - seconds
  - `m` - minutes
  - `h` - hours
  - `d` - days
  - `M` - months (approximate: 30 days)
  - `y` - years (approximate: 365 days)

**Examples**:
- `-1h` - 1 hour ago
- `-30d` - 30 days ago
- `-15m` - 15 minutes ago
- `+2h` - 2 hours from now
- `0` - current time

### Date Formats

Common formats:

| Format | Example Output | Use Case |
|--------|---------------|----------|
| `yyyy-MM-dd'T'HH:mm:ss'Z'` | 2025-01-15T14:30:00Z | ISO 8601 (most APIs) |
| `yyyy-MM-dd` | 2025-01-15 | Date only |
| `yyyy-MM-dd'T'HH:mm:ss.fff'Z'` | 2025-01-15T14:30:00.123Z | With milliseconds |
| `yyyyMMdd` | 20250115 | Compact format |
| `MM/dd/yyyy` | 01/15/2025 | US format |
| `unix` | 1736948400 | Unix timestamp (seconds) |
| `unix_ms` | 1736948400000 | Unix timestamp (milliseconds) |

## Example Scenarios

### 1. Lookback Window (1 hour)

**Use Case**: Poll every 30 minutes, but look back 1 hour to catch delayed updates to CI runs

```yaml
name: ci-updates-lookback
version: 1.0.0
type: poller

date_variables:
  - name: lookback_date
    offset_expression: "-1h"
    format: "yyyy-MM-dd'T'HH:mm:ss'Z'"
    description: "1 hour lookback for delayed updates"

scheduler:
  cron_expression: "*/30 * * * *"  # Every 30 minutes
  server:
    name: ci-api
    address: "https://api.ci.example/runs?updated_after={lookback_date}"
```

**Result** (if current time is 2025-01-15 15:30:00 UTC):
```
https://api.ci.example/runs?updated_after=2025-01-15T14:30:00Z
```

### 2. Time Range Query

**Use Case**: Get runs between start and end dates

```yaml
name: run-time-range
version: 1.0.0
type: poller

date_variables:
  - name: start_date
    offset_expression: "-24h"
    format: "yyyy-MM-dd"
  - name: end_date
    offset_expression: "0"
    format: "yyyy-MM-dd"

scheduler:
  cron_expression: "0 2 * * *"  # Daily at 2 AM
  server:
    address: "https://api.ci.example/runs?from={start_date}&to={end_date}"
```

**Result** (if current time is 2025-01-16 02:00:00 UTC):
```
https://api.ci.example/runs?from=2025-01-15&to=2025-01-16
```

### 3. Backfill Scenario (30 days)

**Use Case**: One-time backfill of 30 days of historical run data

```yaml
name: runs-backfill-30days
version: 1.0.0
type: poller

date_variables:
  - name: backfill_start
    offset_expression: "-30d"
    format: "yyyy-MM-dd'T'HH:mm:ss'Z'"
    description: "Start of backfill period"
  - name: backfill_end
    offset_expression: "0"
    format: "yyyy-MM-dd'T'HH:mm:ss'Z'"
    description: "End of backfill period (now)"

scheduler:
  cron_expression: "0 0 * * *"  # Manual trigger or daily
  server:
    address: "https://api.ci.example/runs?start={backfill_start}&end={backfill_end}"

pagination:
  strategy: "offset-limit"
  page_size: 500
  max_pages: 0  # Unlimited - fetch all pages
```

**Result** (if current time is 2025-01-15 10:00:00 UTC):
```
https://api.ci.example/runs?start=2024-12-16T10:00:00Z&end=2025-01-15T10:00:00Z
```

### 4. Multi-Organization with Different Lookback Windows

**Use Case**: Different organizations have different data delay characteristics

```yaml
name: multi-org-sync
version: 1.0.0
type: poller

# Default lookback: 1 hour
date_variables:
  - name: lookback_date
    offset_expression: "-1h"
    format: "yyyy-MM-dd'T'HH:mm:ss'Z'"

scheduler:
  cron_expression: "*/30 * * * *"

orgs:
  - org_id: org_main
    enabled: true
    # Uses default 1-hour lookback
    
  - org_id: edge_org
    enabled: true
    # Override: This org has 4-hour delays in updates
    date_variables:
      - name: lookback_date
        offset_expression: "-4h"
        format: "yyyy-MM-dd'T'HH:mm:ss'Z'"
        
  - org_id: quicksync
    enabled: true
    # Override: quicksync runs every 15 minutes
    cron_expression: "*/15 * * * *"
    date_variables:
      - name: lookback_date
        offset_expression: "-30m"
        format: "yyyy-MM-dd'T'HH:mm:ss'Z'"
 
endpoints:
  - endpoint_id: Runs
    configuration:
      base_url: "https://api.ci.example/{org_id}/runs?updated_since={lookback_date}"
```

### 5. Combining Static and Date Variables

```yaml
name: mixed-variables
version: 1.0.0
type: poller

# Static variables
configuration:
  environment: prod
  api_version: v2

# Date variables
date_variables:
  - name: since_date
    offset_expression: "-2h"
    format: "yyyy-MM-dd'T'HH:mm:ss'Z'"

scheduler:
  cron_expression: "0 * * * *"  # Hourly
  server:
    # Both types of variables in the URL
    address: "https://api.ci.example/{environment}/{api_version}/changes?since={since_date}"
```

**Result**:
```
https://api.ci.example/prod/v2/changes?since=2025-01-15T12:00:00Z
```

### 6. Unix Timestamp Format

**Use Case**: API requires Unix timestamps

```yaml
name: unix-timestamp-example
version: 1.0.0
type: poller

date_variables:
  - name: start_timestamp
    offset_expression: "-1d"
    format: "unix"  # Unix timestamp in seconds
  - name: end_timestamp
    offset_expression: "0"
    format: "unix"

scheduler:
  server:
    address: "https://api.legacy.com/data?from={start_timestamp}&to={end_timestamp}"
```

**Result**:
```
https://api.legacy.com/data?from=1736861400&to=1736947800
```

## Advanced Patterns

### All Three Variable Types Together

Date variables work seamlessly with **static variables** and **pagination variables**:

```yaml
name: all-variables-example
version: 1.0.0
type: poller

# 1. Static variables
configuration:
  environment: prod
  org_id: ORG001

# 2. Date variables
date_variables:
  - name: lookback_date
    offset_expression: "-2h"
    format: "yyyy-MM-dd'T'HH:mm:ss'Z'"

scheduler:
  server:
    # Uses static + date variables
    address: "https://api.ci.example/{environment}/runs?org={org_id}&since={lookback_date}"

# 3. Pagination variables (handled by strategy)
pagination:
  strategy: "offset-limit"
  page_size: 100
  # Adds: &offset=0&limit=100, &offset=100&limit=100, etc.
```

**Resolution order**: Date variables -> Static variables -> Pagination variables

See [`06-all-variables-complete.yaml`](./06-all-variables-complete.yaml) for a comprehensive example.

### Incremental Sync with State

For production systems, you might want to track the last successful sync time:

```yaml
# Future enhancement: state management
name: incremental-run-sync
version: 1.0.0
type: poller

date_variables:
  - name: last_sync
    offset_expression: "-1h"  # Fallback if no state exists
    format: "yyyy-MM-dd'T'HH:mm:ss'Z'"
    # Future: state_key: "last_run_sync_time"

scheduler:
  server:
    address: "https://api.ci.example/runs?updated_after={last_sync}"
```

### Time Zone Handling

```yaml
date_variables:
  - name: business_hours_start
    offset_expression: "-8h"
    format: "yyyy-MM-dd HH:mm:ss"
    timezone: "America/New_York"  # Eastern Time
    description: "Start of business day in Eastern Time"
```

## Testing Date Variables

To test your docket with specific dates, you can temporarily adjust the offset:

```yaml
# Testing: Simulate running 7 days ago
date_variables:
  - name: test_date
    offset_expression: "-7d"  # 7 days ago
    format: "yyyy-MM-dd"
```

## Best Practices

1. **Overlapping Windows**: Use lookback slightly longer than poll interval to avoid missing data
   - Poll every 30 min ? lookback 1 hour
   - Poll every hour ? lookback 2 hours

2. **Time Zones**: Always use UTC for API integrations unless the API specifically requires local time

3. **Format Consistency**: Match the API's expected date format exactly

4. **Backfill Safety**: Set `max_pages` limit when doing backfills to avoid runaway jobs

5. **Monitoring**: Log resolved URLs to verify date calculations are correct

## See Also

- [Multi-Tenant Examples](../multi-tenant-examples/)
- [Pagination Strategies](../pagination-examples/)
- [Plugin Development](../../docs/PLUGIN_DEVELOPMENT.md)
