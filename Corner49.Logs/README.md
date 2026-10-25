# Corner49.Logs

A standalone .NET CLI that exports Azure App Service / Azure Functions diagnostic logs from an
Azure Storage account into local SQLite databases - one per resource group and month, holding every
parsed, normalized log event - so they can be queried with plain SQL. Point a tool like Claude
Code (or any SQLite client) at the databases to ask questions about your logs.

It works for any app whose Diagnostic Settings archive logs to a storage account: Azure always
writes them into the same `insights-logs-*` containers with the same structure. Nothing in the
tool is specific to one application.

## Usage

```
Corner49.Logs <env> [--data-dir <path>] [--subscription <id>] [--resource-group <rg>] [--site <name>]
                    [--container <name>] [--since <yyyy-MM-dd>] [--until <yyyy-MM-dd>]
```

| Argument / option | Default | Meaning |
|---|---|---|
| `<env>` (required) | - | Environment name, e.g. `dev` or `prod`. Selects the storage account: its connection string is read from the `{env}-connectstring` setting |
| `--data-dir` | `data_dir` setting, else `data` next to the exe | Directory for the SQLite databases. Relative paths are resolved against the executable's folder |
| `--subscription` | (all) | Azure subscription ID the app's logs live under |
| `--resource-group` | (all) | Resource group the app belongs to |
| `--site` | (all) | App Service / Function App site name |
| `--container` | (all 4) | Restrict to a single `insights-logs-*` container |
| `--since` | (incremental) | Only log days (UTC) on/after this date. Every day from here through `--until` is re-checked against Azure, even days already complete. Never earlier than `Export:MaxAgeDays` days ago, nor before the months kept by `Export:RetentionMonths` |
| `--until` | (today) | Only log days (UTC) on/before this date |

```
Corner49.Logs prod
Corner49.Logs dev --data-dir d:\logs --resource-group MY-APP-DEV --since 2026-10-01
Corner49.Logs --help
```

Exit codes: `0` success, `1` configuration error or at least one blob failed, `130` cancelled
with Ctrl+C.

## Configuration

Settings are read from `appsettings.json` (and `appsettings.{DOTNET_ENVIRONMENT}.json`) **next
to the executable** - not the current directory - and from environment variables, which win
over the files.

| Setting | Used for |
|---|---|
| `{env}-connectstring` | Storage account connection string for `<env>`, e.g. `prod-connectstring`, `dev-connectstring` |
| `data_dir` | Data directory when `--data-dir` is omitted (environment variable `DATA_DIR`) |
| `Export:MaxAgeDays` | Hard limit (default `90`) on how far back the exporter goes, whatever `--since` says (environment variable `Export__MaxAgeDays`) |
| `Export:RetentionMonths` | Full months (default `3`) kept besides the current one; older monthly databases are deleted at the end of every export (environment variable `Export__RetentionMonths`) |

```json
{
  "prod-connectstring": "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net",
  "dev-connectstring": "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net",
  "data_dir": "d:\\logs",
  "Export": { "MaxAgeDays": 10, "RetentionMonths": 3 }
}
```

Environment variables (Windows):

```
set prod-connectstring=DefaultEndpointsProtocol=https;AccountName=...
set DATA_DIR=d:\logs
```

Setting names containing `-` can't be exported from bash and most Linux shells; use
`appsettings.json` there.

An unknown `<env>` fails immediately with `No connection string configured for environment
'<env>'`. Connection strings are never logged; only the storage account name is.

## Building and publishing

```
dotnet build Corner49.Logs.csproj
dotnet run --project Corner49.Logs.csproj -- dev
dotnet publish Corner49.Logs.csproj -p:PublishProfile=FolderProfile
```

The project publishes a self-contained single-file `win-x64` executable. `appsettings.json` is
kept beside it (not bundled) so it can be edited after publishing.

## How exports work

Progress is logged as the export runs, and a summary prints at the end:

```
Export completed.
  Files found:            42
  Files exported:         40
  Files skipped:          2
  Files failed:           0
  Lines processed:        125438
  Unparsed lines:         517
  Total events exported:  124921
  Days already complete:  118
```

Azure Diagnostic Settings exports App Service / Function App logs into 4 containers
(`insights-logs-appserviceconsolelogs`, `insights-logs-appservicehttplogs`,
`insights-logs-functionapplogs`, `insights-logs-appserviceapplogs`), each laid out as
`resourceId=/SUBSCRIPTIONS/{subId}/RESOURCEGROUPS/{rg}/PROVIDERS/MICROSOFT.WEB/SITES/{site}/y=/m=/d=/h=/m=00/*.json`.
Sites are discovered by listing those levels (skipping the ones pinned by
`--subscription`/`--resource-group`/`--site`), and days are listed with a per-day `y=/m=/d=`
prefix, so a run never lists more than it checks.

**Incremental exports**: without `--since`, a run works out per site and container which UTC
days to check from what's already in the resource group's monthly databases:

- **Never exported before**: the last `Export:MaxAgeDays` days through today are exported.
- **Otherwise**: every day that already has blobs in the database, plus every day from the
  newest one through today (UTC), so a gap since the last run is filled in.
  - A day that was already listed *after* it ended (plus a 2 hour settle margin, since Azure
    keeps appending to an hour's blob for a bit), with no failed blobs, is complete and isn't
    listed again ("Days already complete").
  - Any other day is listed in Azure and compared to the `blobs` table: blobs that are
    missing, have a new ETag, or previously failed are (re-)exported. Unchanged blobs are
    skipped.

One blob failing (auth error, malformed content, network issue) doesn't stop the others; it's
recorded in `blobs` with `status = 'Failed'` and an `error_message`, and retried on the next run.

**Retention**: at the end of every export, the monthly databases of months more than
`Export:RetentionMonths` full months back are deleted, in every resource group folder of the data
directory (with `3`, a run in October keeps July through October and deletes June and older).

## Databases

Every resource group in the blob path gets its own folder in the data directory, with one SQLite
database per month, all named after the resource group in lowercase:

```
d:/logs/
  my-app-dev/                    <- MY-APP-DEV
    my-app-dev-2026-09.db
    my-app-dev-2026-10.db
  my-app-prd/                    <- MY-APP-PRD
    my-app-prd-2026-10.db
```

The resource group/service/date/hour of a blob come from the blob's own name (see
`Export/BlobPathSegments.cs`), not from parsed line content, so every blob lands in a
deterministic place even if every line inside it fails to parse. A blob goes into the database
of its blob path's month (the `y=/m=` partition), so a day's blobs, events and checks are always in
one file; an event logged just before midnight on the last day of a month can sit in the next
month's database if Azure wrote it to that hour's blob. To query across months, `ATTACH` the other
files or `UNION ALL` over them.

Single-file databases of older versions (`{data dir}/{resource group}.db`) are no longer used:
a run logs a warning while any `*.db` file sits directly in the data directory, and they can be
deleted.

Each blob is imported in a single transaction: its `blobs` row is upserted, its previous events
deleted and the new ones inserted, and only then committed. The databases use WAL mode, so you
can query them while an export is running. Dates are `yyyy-MM-dd` text and timestamps ISO-8601
UTC text, so both sort correctly.

### Tables

- **`events`** - one row per parsed log line: `blob_id` (-> `blobs.id`), `service`, `timestamp`,
  `level`, `category`, `message`, `span_id`, `trace_id`, `request_id`, `action_name`,
  `event_type`, `http_method`, `http_path`, `status_code`, `duration_ms`, `source_blob`,
  `source_line_number`, `imported_at`, `properties` (JSON object with every other state/scope
  value, `NULL` when empty - query it with `json_extract`), `route`, `user_id` and `exception`
  (full exception text incl. stack trace). Indexed on `timestamp`, `(service, timestamp)`,
  `(level, timestamp)`, `(category, timestamp)`, `trace_id`, `route`.
- **`blobs`** - one row per exported blob, unique on `(container, blob_name)`: `service`,
  `date`, `hour`, `etag`, `blob_size`, `last_modified`, `exported_at`, `event_count`,
  `unparsed_count`, `status` (`Success`/`Failed`) and `error_message`.
- **`day_checks`** - when each `{container}/{service}` was last listed in Azure for a day
  (`date`, `check_key`, `checked_at`). Only recorded for days that have blobs.

The schema version is stored in `PRAGMA user_version` (currently `2`). Databases created by the
older EasyRes.Logs keep their `restaurant_id` / `booking_id` / `guest_id` columns, but those
are no longer filled.

### Console log mapping

Structured console lines (the .NET JSON console formatter) are mapped as follows:

- `LogLevel`, `Category`, `Message`, `Exception`, `Timestamp` -> their columns. Timestamps in
  `dd-MM-yyyy HH:mm:ss.fffzzz`, `dd-MM-yyyy HH:mm:ss` or any invariant/ISO-8601 format are
  understood; otherwise the envelope's `time` is used.
- Scope keys `TraceId`, `SpanId`, `RequestId`, `RequestPath`, `ActionName`, `Route`, `UserId`
  (case-insensitive) -> their columns. All other scope and state values -> `properties`.
- Request-logging lines: when the state carries `Method`, `StatusCode`, `ElapsedMs` (or
  `DurationMs`) and `Route`, they fill `http_method`, `status_code`, `duration_ms` and `route`,
  whatever the category.

### Example queries

```sql
-- Errors with stack traces
SELECT timestamp, service, route, message, exception FROM events
WHERE exception IS NOT NULL ORDER BY timestamp DESC LIMIT 20;

-- Errors per service in the last day
SELECT service, count(*) FROM events
WHERE level IN ('Error', 'Critical') AND timestamp >= datetime('now', '-1 day')
GROUP BY service ORDER BY 2 DESC;

-- Slowest routes (application request logs)
SELECT service, route, count(*) AS calls, avg(duration_ms) AS avg_ms, max(duration_ms) AS max_ms
FROM events WHERE event_type = 'Console' AND duration_ms IS NOT NULL
GROUP BY service, route ORDER BY avg_ms DESC LIMIT 20;

-- Everything for one request
SELECT timestamp, service, level, message FROM events WHERE trace_id = '...' ORDER BY timestamp;

-- Filter on an app-specific scope value
SELECT timestamp, message FROM events WHERE json_extract(properties, '$.OrderId') = '...';

-- Blobs that failed to export
SELECT container, blob_name, error_message FROM blobs WHERE status = 'Failed';
```

## Project architecture

```
CLI/            System.CommandLine root command (RootCommandFactory) and host/DI setup per run (LogsHost).
Configuration/  Options classes (AzureStorageOptions, ExportOptions).
Domain/         Plain POCOs (LogEvent) and parser-side enums (LogEventLevel, LogEventType).
Storage/        ILogStorageReader abstraction + the Azure Blob Storage implementation.
Parsing/        ILogParser implementations - see "Adding a new log parser" below.
Export/         LogExporter (orchestration) and the per-resource-group, per-month SQLite databases under Export/Database/.
Infrastructure/ Output directory resolution, connection string redaction.
```

Data flow:

```
CLI (RootCommandFactory -> LogsHost)
  -> LogExporter
    -> ILogStorageReader (lists/opens blobs)
    -> LogParserDispatcher -> ILogParser implementations (parses each line)
    -> LogDatabaseFactory -> LogDatabase (one per resource group and month: skip-if-unchanged, day completeness)
      -> BlobImport (one transaction per blob: blob row + its events)
```

### The log format

`Logs/*.json` holds real sample exports from Azure Monitor diagnostic settings. Every line is
one JSON object with a common envelope: `time`, `resourceId` (ARM path - service and resource
group come from it) and `category`, which selects the parser:

- **`AppServiceConsoleLogs`** (`ConsoleLogParser`) - payload `resultDescription`, a JSON
  *string*: usually a structured ILogger line, sometimes plain text (startup banners,
  `Console.WriteLine`) - both handled.
- **`AppServiceHTTPLogs`** (`HttpLogParser`) - payload `properties`, a JSON string with
  IIS/W3C-style fields (`CsMethod`, `CsUriStem`, `ScStatus`, `TimeTaken`, ...). Level is derived
  from `ScStatus` (2xx/3xx Information, 4xx Warning, 5xx Error).
- **`FunctionAppLogs`** (`FunctionAppLogParser`) - outer `time` is `MM/dd/yyyy HH:mm:ss` and
  `properties` is single-quoted, Python-repr-style text, so this parser uses tolerant regex
  extraction.
- Anything else falls through to `GenericFallbackLogParser`, which never throws and tags the
  event `Unparsed` - nothing is silently dropped.

### Adding a new log parser

Implement `Parsing.ILogParser`:

```csharp
public sealed class MyNewLogParser : ILogParser
{
    public bool CanParse(string line) => /* cheap, non-throwing check */;
    public LogEvent? Parse(string line, string sourceBlob, long lineNumber) => /* map fields */;
}
```

and register it in `CLI/LogsHost.cs` alongside the existing parsers:

```csharp
builder.Services.AddSingleton<ILogParser, MyNewLogParser>();
```

`LogParserDispatcher` uses the first registered parser whose `CanParse` returns true.
`Parsing/AzureDiagnosticEnvelope.cs` has the shared envelope helpers (timestamp, resourceId,
JSON element access) - reuse them for any other Azure diagnostic format.
