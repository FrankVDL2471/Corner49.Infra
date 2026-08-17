# Corner49.Infra

> Reference document for AI assistants (and humans) working in a project that consumes the `Corner49.Infra` NuGet package. It explains what the library does, how its pieces fit together, and — critically — how the underlying Azure services (Cosmos DB, Service Bus, Blob Storage, Hangfire) are wired up internally, so you can use the package in the way it was designed to be used instead of fighting it or re-implementing things it already does.

## What this is

`Corner49.Infra` is Frank Vanderlinden's opinionated infrastructure library for ASP.NET Core (.NET 10) applications. It is a thin, batteries-included wrapper around a fixed stack of Azure services:

- **Azure Cosmos DB** (NoSQL API) — document storage, via `DocumentRepo<T>`
- **Azure Service Bus** — queues/topics for async messaging, via `IServiceBusService`
- **Azure Blob Storage** — file storage, via `IBlobService`
- **Hangfire** (backed by Cosmos DB or SQL Server) — background jobs / cron
- **Auth0** — authentication (API JWT bearer + interactive web app login)
- **Application Insights** — logging/telemetry
- **Azure SignalR** — realtime hubs
- **Azure App Configuration** — centralized config

The entry point is a fluent builder (`InfraBuilder`) attached to `WebApplicationBuilder`/`HostApplicationBuilder` via `.UseInfra(appName)`. Consuming projects call `.With...()` / `.Add...()` methods to opt into the pieces they need, then `await infra.BuildAndRun()`.

Source layout (namespaces mirror folders):

```
Corner49.Infra/
  InfraBuilder.cs / ServiceExtensions.cs   – the fluent bootstrap API
  DB/            – Cosmos DB repository layer (IDocumentDB, DocumentRepo<T>)
  ServiceBus/    – Azure Service Bus client, sender, hosted-service trigger
  Messages/      – higher-level typed pub/sub built on top of ServiceBus
  Jobs/          – Hangfire wrapper (cron + fire-and-forget jobs)
  Storage/       – Azure Blob Storage wrapper
  Auth/          – Auth0 integration (JWT bearer + Management API client)
  ApiKey/        – simple API-key auth filter for controllers
  Http/          – ApiClient base class for calling other HTTP APIs
  Health/        – health-check aggregation
  Logging/       – App Insights / console logging setup
  Helpers/, Tools/ – small utilities (JSON defaults, base36/62/64, hashing, etc.)
```

## Bootstrap pattern

Every consuming app starts the same way, in `Program.cs`:

```csharp
using Corner49.Infra;

var infra = WebApplication.CreateBuilder(args)
    .UseInfra("MyAppName")                 // reads appsettings*.json + env vars + optional Azure App Config
    .WithApiControllers()                  // or .WithViewControllers() for MVC/Razor apps
    .WithLogging(c => {
        c.WriteToConsoleAsJson = true;
        c.FilterCategoryPrefix = new[] { "Corner49" };
    })
    .WithAuth0()
    .WithHealthCheck()
    .AddServiceBus()
    .AddDocumentDB(bld => {
        bld.AddRepo<IDataRepo, DataRepo>();
    });

infra.Services.AddSingleton<IMyService, MyService>();

await infra.BuildAndRun();
```

`InfraBuilder` methods are chainable and mostly idempotent-configuration (they call `services.Configure<T>()` / `AddSingleton` under the hood). Order matters only in that `WithApiControllers`/`WithViewControllers` must run before things that register MVC filters, and `AddDocumentDB`/`AddJobs` should run before `BuildAndRun` since repo/database initialization happens inside `BuildAndRun`/`Run`.

**`UseInfra` also builds the configuration pipeline**: it loads `appsettings.json` → `appsettings.{Environment}.json` → `appsettings.{MachineName}.json` → environment variables, and if an `AppConfig` connection string is present in that local config, it layers in Azure App Configuration on top (filtered by environment name and machine name as labels). Local `appsettings.*.json` always wins over anything from Azure App Configuration — this is a deliberate override order so a developer's local file can always take precedence.

`InfraBuilder.Instance` is a static holder (`Instance.Services`) populated after the host is built — mainly useful for static/non-DI code paths that need to resolve a service post-startup.

## Cosmos DB (`Corner49.Infra.DB`)

### Concepts

- **`IDocumentDB`** — a singleton wrapping a `CosmosClient`. Registered via `AddDocumentDB`. `CosmosClient` instances are cached in a static `ConcurrentDictionary` keyed by `connectionString|directMode`, so multiple `IDocumentDB`/repo instances that share a connection string reuse the same underlying `CosmosClient` (and its TCP/connection pool) even across DI scopes — **do not create your own `CosmosClient`s**, always go through `IDocumentDB`.
- **`DocumentRepo<T>`** — one per (database, container, partition key path(s)) combination. This is the actual repository; it implements `IDocumentRepo<T>` and `IDocumentRepoInitializer`.
- **`DocumentDBBuilder`** — the fluent registration surface passed to `AddDocumentDB(bld => ...)`. Call `bld.AddRepo<IMyRepo, MyRepo>()` for each repo interface/implementation pair. Repos are registered as **singletons**.
- Container/database creation happens automatically: during `BuildAndRun`/`Run`, the framework calls `Init()` on every registered `IDocumentRepoInitializer`, which does a `CreateDatabaseIfNotExistsAsync` + `CreateContainerIfNotExistsAsync` (retrying on 429 Too Many Requests). **You never need to manually provision Cosmos databases/containers** for repos registered through `AddDocumentDB` — just declare the repo and its partition key(s) and the container is created on startup if missing, with `DefaultTimeToLive = -1` (TTL enabled per-item, off by default).

### Configuration

Section name: `CosmosDB` (bound to `DocumentDBOptions`).

```json
{
  "CosmosDB": {
    "ConnectString": "AccountEndpoint=https://...;AccountKey=...;",
    "DatabaseName": "data",
    "DirectMode": false
  }
}
```

- `DatabaseName` is the **default** database for repos that don't override it via `dbName`.
- `DirectMode` toggles Cosmos `ConnectionMode.Direct` vs the default `Gateway`. Direct mode also sets `IdleTcpConnectionTimeout = 1h`. Gateway (the default) works everywhere including behind restrictive firewalls; Direct mode is lower-latency but needs more open ports — only enable it if you know the network allows it.
- `AllowBulkExecution = true` is always set on the client (enables efficient parallel bulk operations used by `BulkInsert`/`BulkUpdate`/`BulkDelete`).
- Serialization always goes through `JsonCosmosSerializer`, which uses `System.Text.Json` with `Corner49.Infra.Tools.JsonHelper`'s defaults: **camelCase property names**, enums serialized as camelCase strings, `null`s omitted on write, numbers can be read from strings. This means your document POCOs don't need `[JsonPropertyName]` attributes for camelCase — just use PascalCase C# properties normally. Respect `[JsonPropertyName]` if you need a different wire name (e.g. `id` for the Cosmos system property).

### Defining a repo

```csharp
public interface IDataRepo {
    Task<DataModel?> GetItem(string pk, string id);
    Task<QueryResult<DataModel>> Query(Func<IQueryable<DataModel>, IQueryable<DataModel>> query);
}

public class DataRepo : IDataRepo, IDocumentRepoInitializer {
    private readonly DocumentRepo<DataModel> _repo;

    public DataRepo(IDocumentDB db) {
        // GetRepo<T>(containerName, params partitionKeyPaths)
        // uses the default DatabaseName from DocumentDBOptions
        _repo = db.GetRepo<DataModel>("Parts", "partitionKey");

        // optional: observe RU charge / latency per call
        _repo.OnDiagnostics = (diag) => {
            Console.WriteLine($"{diag.Method} {diag.ElapsedTime?.TotalMilliseconds}ms, {diag.TotalRequestCharge} RU");
            return Task.CompletedTask;
        };
    }

    Task IDocumentRepoInitializer.Init() => _repo.Init();

    public Task<DataModel?> GetItem(string pk, string id) => _repo.GetItem(pk, id);
    public Task<QueryResult<DataModel>> Query(Func<IQueryable<DataModel>, IQueryable<DataModel>> query)
        => _repo.Query((string?)null, query);
}
```

Register it: `bld.AddRepo<IDataRepo, DataRepo>()` inside `AddDocumentDB`. If you need a non-default database/container name, pass options: `bld.AddRepo<IDataRepo, DataRepo>(o => { o.DatabaseName = "other"; o.ContainerName = "parts"; })` — this is delivered to the repo's constructor as a `DocumentRepoOptions` parameter (resolved via `ActivatorUtilities`, so add a `DocumentRepoOptions` constructor parameter to receive it).

`DocumentRepoOptions` also carries `DatabaseAutoscaleThroughput`/`ContainerAutoscaleThroughput` (nullable `int`, autoscale max RU/s). These aren't applied automatically — set them on the `DocumentRepo<T>` instance in your repo's constructor before `Init()` runs at startup: `_repo.ContainerAutoscaleThroughput = options?.ContainerAutoscaleThroughput;`. Left unset, `Init()` creates the database/container with no explicit throughput, which on a non-serverless account is a common cause of 429s.

Partition keys can be **hierarchical** — pass multiple path segments to `GetRepo<T>(containerName, "tenantId", "userId")` and use the `string[]` overloads of every method (`GetItem(string[] pk, id)`, `Query(string[] pk, ...)`, etc.). Every operation has both a `string` (single partition key) and `string[]` (hierarchical) overload.

### API surface & when to use what

`DocumentRepo<T>` / `IDocumentRepo<T>` exposes:

| Method | Use for | RU/perf notes |
|---|---|---|
| `GetItem(pk, id)` / `ReadItem(pk, id)` | point read by id + partition key | Cheapest possible op (~1 RU for small docs). `ReadItem` uses the stream API; `GetItem` uses the typed API. Prefer these over queries whenever you know the id. |
| `AddItem(pk, item)` | insert, fails if id exists | |
| `UpsertItem(pk, item, status)` | insert-or-replace | more efficient than read-then-write; `status` callback tells you 201 (created) vs 200 (replaced) |
| `PatchItem(pk, id, patches)` | partial update via `PatchOperation` list | cheaper than full replace — reduces payload and RU |
| `DeleteItem(pk, id)` | delete | returns `false` (not throw) on 404 |
| `CreateQuery(pk?, maxItemCount?)` | build a raw LINQ `IQueryable<T>` | pass `pk` whenever possible — omitting it is a **cross-partition fan-out query** |
| `GetItems(pk?)` | stream all docs in a partition (or all, if `pk` is null) as `IAsyncEnumerable<T>` | memory-efficient, avoid for huge cross-partition scans |
| `Query(pk?, queryBuilder, continuationToken?, maxItemCount?)` | LINQ query **with pagination** (`QueryResult<T>`: `Data`, `TotalCount`, `ContinuationToken`) | `TotalCount` is only computed when `continuationToken == null` (first page) — don't expect it on subsequent pages |
| `Query(pk?, sql, continuationToken?, maxItemCount?, parameters?)` | raw parameterized SQL, paginated | always use `@param` placeholders, never string-concatenate values into `sql` |
| `Filter(pk?, DocumentFilter<T>, cancelToken)` | same as `Query` but driven by a `DocumentFilter<T>` object (override `Build(IQueryable<T>)`, has `Search`, `Take`, `ContinuationToken`) — a convenient pattern for exposing filterable/pageable list endpoints | |
| `Read(pk?, queryBuilder, onRead callback, maxItemCount?)` | stream results page-by-page via callback (`onRead` returns `false` to stop early) | use for large exports/batch processing instead of materializing `QueryResult.Data` |
| `ReadSQL(pk?, sql, onRead, continuationToken?, ...)` | same streaming pattern but raw SQL | |
| `ExecSQL<M>(pk?, sql, ...)` | SQL query projected into a **different** type `M` (e.g. `SELECT c.name, c.status FROM c`) as `IAsyncEnumerable<M>` | use when you only need a few fields — cheaper than pulling whole docs |
| `RawSQL(pk?, sql, ...)` | returns raw `JsonElement`s — for dynamic/unknown-shape results | |
| `CountSQL(pk?, whereClause)` | `SELECT COUNT(1)` with an optional WHERE (no `WHERE` keyword) | cross-partition if `pk` is null — expensive, avoid calling per-request if avoidable |
| `BulkInsert` / `BulkUpdate` / `BulkDelete` | fire-and-forget-style bulk ops over `IAsyncEnumerable<T>`, using Cosmos bulk execution | fire many concurrent requests via `Task.WhenAll` — fine for background/migration jobs, be mindful of RU budget on the container |
| `GetChangeFeedProcessor` / `GetAllChangesFeedProcessor` | Cosmos DB **change feed** — event-driven reaction to inserts/updates on a container | creates/uses a lease container (default name `changeLeases`); call `.StartAsync()` on the returned `ChangeFeedProcessor`. Use for cache invalidation, fan-out to Service Bus, search-index sync, etc. |
| `Exists()` | check if the container exists (cached after first call) | |

**Performance guidance baked into the API (follow these when writing against it):**
1. Always pass the partition key when you have it — every method accepting `null` for `partitionKey` is doing a cross-partition query.
2. Prefer `GetItem`/`ReadItem` (point reads) over `Query`/`ExecSQL` when you already know the id.
3. For large result sets, prefer the streaming variants (`Read`, `ReadSQL`, `GetItems`, `ExecSQL`) over `Query`, which buffers everything into `QueryResult<T>.Data`.
4. Use `PatchItem` instead of read-modify-`UpsertItem` when you're only changing a few fields.
5. `ContinuationToken`s returned by `Query`/`Filter` are already Base64-wrapped (`Base64.Encode`/`Decode` internally) — treat them as opaque strings, pass them straight back in on the next page request.
6. Retries: point reads/writes automatically retry up to 3 times on `429 TooManyRequests` / `408 RequestTimeout` (honoring `Retry-After`), and `Init()` retries up to 5 times on `429`. You generally don't need your own retry wrapper for these statuses.
7. Attach `OnDiagnostics` on a repo (per instance) during development/perf investigations to see RU charge and latency per call without needing to inspect Cosmos metrics in the portal.
8. All repos from `AddDocumentDB` are singletons and share one cached `CosmosClient` per connection string — this is intentional (Cosmos SDK guidance is to reuse `CosmosClient`), don't try to scope them per-request.

### Errors

Cosmos-related failures surface as `DocumentException` (with `StatusCode`) or the more specific `DocumentContainerNotFoundException` (thrown if you call repo methods before `Init()` has run / the container isn't set up). 404s on reads/deletes are **not** exceptions — `GetItem`/`ReadItem` return `null`, `DeleteItem` returns `false`.

## Service Bus (`Corner49.Infra.ServiceBus`)

### Concepts

- **`IServiceBusService`** — singleton wrapping one `ServiceBusClient`/`ServiceBusAdministrationClient` pair for the whole app (registered automatically the moment you call `.UseInfra(...)`, even before `AddServiceBus()`). It handles: queue/topic/subscription **auto-creation**, senders, processors, dead-letter management, and message counts.
- **`AddServiceBus(cfg => ...)`** binds `ServiceBusConfiguration` (section `ServiceBus`) — mainly the connection string and `DeveloperMode`/`IsBasicTier`/`MaxDeliveryCount` switches.
- **`AddServiceBusHandler<T>(opt => ...)`** where `T : IServiceBusHandler` registers a `ServiceBusTrigger<T>` as an `IHostedService` — this is a long-running background listener that pulls messages off a queue/subscription and calls `T.MessageReceived(ServiceBusCommand)` for each one, inside a fresh DI scope per message.
- **`Corner49.Infra.Messages`** is a higher-level, more opinionated layer on top of raw `IServiceBusHandler`/`ServiceBusCommand`: strongly-typed messages (`MessageBase` subclasses) with an `Action` string that gets method-dispatched on the receiving side (`MessageHandler<T>`), and a typed sender (`MessageService<T>`). **Prefer `Messages` over raw `ServiceBus` types for new application-level pub/sub** — it gives you compile-time-checked payloads and automatic method routing; drop to raw `ServiceBusCommand`/`IServiceBusHandler` only for interop or very simple fire-and-forget signals.

### Queue/Topic auto-provisioning & naming — important behavior

Names are **always lower-cased**. The service auto-creates entities on first use (caching known names in-process to avoid repeated admin calls):

- Queues: `LockDuration=5m`, `DefaultMessageTimeToLive=7d`, `DeadLetteringOnMessageExpiration=true`, `MaxDeliveryCount` from config (default 10).
- Topics: `DefaultMessageTimeToLive=7d`, `MaxSizeInMegabytes=5120`, `AutoDeleteOnIdle=30d`.
- Subscriptions: `MaxDeliveryCount` from config, `LockDuration=5m`, `DeadLetteringOnMessageExpiration=true`. If you pass a `SubscriptionFilter` (SQL filter string) and an existing subscription's filter differs from what you asked for, **the subscription is deleted and recreated** with the new filter — be aware this drops any messages currently sitting in that subscription.
- Duplicate detection (`RequiresDuplicateDetection`) is only enabled when you pass a `DuplicateDetectionWindow` **and** `IsBasicTier` is `false` for queues (Basic tier Service Bus namespaces don't support duplicate detection at all).

**`DeveloperMode` (set in `ServiceBusConfiguration`) is designed so multiple developers can share one Service Bus namespace without stealing each other's messages:**
- For **queues**: the machine name is appended to the queue name (`myqueue.DEVBOX1`, truncated to 50 chars), so each dev gets their own physical queue.
- For **topics**: instead of a separate topic, a subscription is created with a SQL filter `Target = '<MachineName>'`, and `ServiceBusMessageSender` automatically stamps outgoing messages with `Target = Environment.MachineName` when `DeveloperMode` is on and no `Target` was already set. So each developer's local process only receives messages addressed to them.
- Also drops `MaxConcurrentCalls` expectations down in `DEBUG` builds when using `AddMessageHandler` (see below).

Turn `DeveloperMode` on for local/dev environments, off for staging/production.

### Sending messages

Low-level (`IServiceBusService` injected directly, e.g. in a controller):

```csharp
var sender = _serviceBus.GetTopicSender("topictest");   // or GetQueueSender(name)
var cmd = new ServiceBusCommand {
    Name = "Generated",
    Source = "SampleAPI",
    Target = "All",
};
cmd.SetData(payload);           // serializes as camelCase JSON, records DataType for round-tripping
await sender.Send(cmd);
// or: await sender.Send(cmd, scheduleTime);       // deferred/scheduled delivery
// or: await sender.Send(listOfCommands);           // batched send
```

`ServiceBusCommand` maps to `ServiceBusMessage` as: `Name → Subject`, `PartitionKey → PartitionKey` (set this for session/partition affinity on partitioned entities), `Source`/`Target`/`Timestamp`/`DataType`/current OS `User` → `ApplicationProperties`, `MessageId → MessageId` (used for **duplicate detection** — set it deterministically if you rely on dedup).

High-level (`Messages` layer — preferred for app messages):

```csharp
public class DataMessage : MessageBase {
    public DataMessage() : base("data") {}   // "data" = topic name; pass useQueue:true for a queue instead
    public DataModel? Payload { get; set; }
}

public interface IDataMessageService {
    Task Created(DataModel item);
}

public class DataMessageService : MessageService<DataMessage>, IDataMessageService {
    public DataMessageService(ILogger<DataMessageService> logger, IServiceProvider sp) : base(logger, sp) {}
    public Task Created(DataModel item) {
        var msg = new DataMessage { Action = nameof(Created), Payload = item };
        return base.Send(msg);
    }
}
```
Register with `infra.Services.AddSingleton<IDataMessageService, DataMessageService>()`. `MessageService<T>.Send` also has a `Send(msg, throttleDelaySeconds)` overload that coalesces bursts of the same `MessageId` into one delayed send (in-process throttle dictionary — not distributed, only helps within a single instance).

### Receiving messages

Low-level handler:

```csharp
public class BusHandler : IServiceBusHandler {
    public Task MessageReceived(ServiceBusCommand msg) {
        // msg.GetData<T>() to deserialize the payload
        return Task.CompletedTask;
    }
}
// Program.cs:
infra.AddServiceBusHandler<BusHandler>(cfg => {
    cfg.Name = "samplequeue";
    cfg.Kind = ServiceBusKind.Queue;     // or .Topic (then set SubscriptionName / SubscriptionFilter)
    cfg.MaxConcurrentCalls = 30;          // default; how many messages processed in parallel
    cfg.PrefetchCount = 0;                // default; raise to improve throughput on fast consumers
    cfg.TrackMessageCount = true;         // populates msg.MessageCount (adds an admin API call per message — use sparingly)
});
```

High-level handler via `Messages`:

```csharp
public class DataMessageHandler : MessageHandler<DataMessage> {
    public Task Created(DataModel item) { ... }   // auto-dispatched when Action == "Created"
    public Task Updated(DataModel item) { ... }   // auto-dispatched when Action == "Updated"
}
// Program.cs:
infra.AddMessageHandler<DataMessage, DataMessageHandler>();
```
`MessageHandler<T>.MessageReceived` reflection-dispatches to a method on your handler whose name matches `cmd.Name`/`Action` and whose single parameter type matches the deserialized payload — name your handler methods exactly like the sender's `Action` strings.

**Receiving is a hosted background service (`ServiceBusTrigger<T>`), one per `AddServiceBusHandler`/`AddMessageHandler` call.** It:
- Creates a new DI scope **per message** (so scoped services like `DbContext`-style repos work correctly).
- Uses `ReceiveAndDelete` mode for queues (message is removed as soon as it's handed to your code — if your handler throws, the message is **gone**, not retried; if you need at-least-once/retry semantics, handle errors inside your handler or hold DLQ workflow manually) — subscriptions use `PeekLock`-equivalent processor defaults with `MaxAutoLockRenewalDuration = 30m`.
- Registers itself with `HealthService` — its `IsRunning`/`IsProcessing` state contributes to the app's `/health` endpoint automatically once you call `.WithHealthCheck()`.
- Wraps each message in an Application Insights `RequestTelemetry` operation named `SB-GET {name}/{cmd.Name}/{cmd.MessageId}`, propagating the `Diagnostic-Id` property as the parent operation id if present (so you get end-to-end distributed tracing from sender to receiver in App Insights automatically, no extra code needed).
- Logs and swallows handler exceptions (via `ProcessErrorAsync`/try-catch around `MessageReceived`) rather than crashing the host.

### Dead-lettering & operational helpers

`IServiceBusService` also exposes:
- `GetMessageCount(options)` — active or dead-letter count (`options.DealLetter = true`) for a queue/subscription.
- `ResubmitDeadletterQueue(options)` — drains a queue's dead-letter sub-queue back onto the live queue, preserving message properties; blocks until the DLQ (as counted at call time) is drained.
- `DeleteQueue(name)` — deletes a queue and forgets it from the in-process "known queues" cache.
- `IsTopicFull(name)` — true if a topic has <10MB of its `MaxSizeInMegabytes` quota left; useful as a guard before high-volume publishing.

## Jobs (`Corner49.Infra.Jobs`) — Hangfire

`AddJobs(builder, config)` wires up **Hangfire** with a storage backend chosen by `JobConfig.UseSqlServer`:
- `false` (default) — **Cosmos DB storage** (`Hangfire.AzureCosmosDB`), reusing the connection string's account/key (parsed via `Corner49.Infra.Helpers.CosmosDBHelper`) — `ContainerName` defaults to `"jobs"` in `InfraBuilder.AddJobs`.
- `true` — SQL Server storage (`Hangfire.SqlServer`), pointing at `JobConfig.ConnectString`/`DbName`.

```csharp
infra.AddJobs(bld => {
    bld.AddCronJob<TestJob>(cron => cron.EveryMinute(5));
    bld.AddJob<IMyJobService, MyJobService>();
}, cfg => {
    cfg.UseLocalQueue = true;      // dev convenience: each machine gets its own Hangfire queue (its lower-cased machine name)
    cfg.QueueName = "test";        // shared queue name when not using UseLocalQueue
    cfg.DbName = "jobs-dev";
    cfg.EnableDashboard = true;    // exposes Hangfire dashboard at /jobs (no auth by default — JobAuth.Authorize always returns true, lock this down at the network/reverse-proxy level)
});
```

- **Cron jobs**: subclass `JobRunner`, implement `Execute(args, cancellationToken)`, register with `AddCronJob<T>(cron => ...)`. `CronBuilder` is a tiny fluent cron-expression builder (`EveryMinute(n)`, `EveryHour(n)`, `WithHour`, `WithMinute`, `WithDayOfMonth`) — for anything more complex, you can still hand Hangfire a raw cron string.
- **On-demand jobs**: register with `bld.AddJob<IMyService, MyServiceImpl>()` (implementation extends `JobRunner`), then resolve `IJobManager` and call `StartJob<T>(args, queue)` to enqueue a fire-and-forget execution. `IJobManager.GetJobs()`/`GetJobStatus(id)` expose processing/failed job introspection.
- `JobRunner.Run` wraps `Execute` with `[AutomaticRetry(Attempts = 0)]` — **jobs do not auto-retry on failure by default**; handle retry logic yourself inside `Execute` if you need it, or override the attribute.
- The dashboard is mounted by `InfraBuilder.BuildAndRun` automatically (`_jobs.UseDashboard(app, appName)`) when `EnableDashboard` is true.
- `DisableAutomaticRestart = false` re-queues any jobs left "processing" by servers that no longer exist (e.g. after a deploy killed the previous instance mid-job) — useful in production, defaults to `true` (disabled) in `JobConfig`'s constructor but the sample turns it on.

## Blob Storage (`Corner49.Infra.Storage`)

`IBlobService` is registered **keyed by name** (multiple storage accounts/containbackground purposes can coexist):

```csharp
services.AddBlobService("Documents");   // reads Storage:Documents:ConnectString (+ optional Storage:Documents:CDN) from config
// resolve:
var blobs = serviceProvider.GetBlobService("Documents");
```
or construct directly with a connection string: `new BlobService(connectString)`.

Config shape:
```json
{ "Storage": { "Documents": { "ConnectString": "...", "CDN": "https://cdn.example.com" } } }
```

Key behaviors:
- Containers are created on first write (`GetContainer(name, createIfNotExists: true)`) with **public Blob access**, and container names are sanitized/lower-cased via `FormatContainerName` (also strips underscores) to satisfy Azure container-naming rules — don't pre-sanitize names yourself, pass human-readable names in.
- `GetCDN(container, name)` builds a public URL as `{CDN}/{container}/{name}` — if you configured a `CDN` host, use `Upload(...)`'s returned URL directly in your app rather than round-tripping through blob SDK URLs.
- `Upload(...)` overloads accept `IFormFile`, `Stream`, `byte[]`, or base64 `string`; each **deletes any existing blob with that name first** (full overwrite semantics, not upsert-merge).
- `Append`/`AppendText`/`CreateText` use Append Blobs for log-like/streaming write patterns; `AppendText` automatically rolls over to `name-part1`, `name-part2`, ... when a blob hits Azure's max block count.
- `GetItems(container, path)` does hierarchical (virtual-folder) listing — yields blob names and `"prefix/"` pseudo-folder entries, matching Azure's `GetBlobsByHierarchyAsync`.
- `MoveFile` is copy-then-delete-source (no native server-side move) — fine for small/medium files, consider a Cosmos change-feed/queue-driven move for very large blobs.

## Auth (`Corner49.Infra.Auth`) — Auth0

`WithAuth0(opt => ...)` configures Auth0 based on `Auth0:Domain/ClientId/ClientSecret/Audience/ApiIdentifier` config (or the lambda). Behavior branches on which controller types you've registered:
- If `WithApiControllers()` was called: sets up **JWT Bearer** authentication (`Authority = https://{Domain}/`, `Audience` required) — for APIs validating Auth0-issued access tokens.
- If `WithViewControllers()` was called: sets up **Auth0 interactive web app login** (`AddAuth0WebAppAuthentication`, cookie-based) — for MVC/Razor apps with a login redirect flow. Requires `ClientId`.
- Both can be active simultaneously in the same app (API + MVC front-end).

`Auth0Service` (`IAuth0Service`) is a thin client for the **Auth0 Management API** — `GetCurrentUser(HttpContext)` (via the caller's bearer token → `/userinfo`), `GetUser(email)`, `DeleteUser(email)` (via a client-credentials machine token). Requires `ApiIdentifier`/`ClientSecret` to be set for the machine-token calls.

Alternative: **`WithApiKey<T>()`** where `T : IApiKeyValidation` registers a simple `[ApiKey]` action/controller filter that checks the `X-API-Key` header (or `?apiKey=` query string) against your `IsValidApiKey(string)` implementation — use this for service-to-service or webhook endpoints instead of full OAuth.

## Calling other HTTP APIs (`Corner49.Infra.Http.ApiClient`)

`ApiClient` is a base class to derive typed clients from (see `Corner49.Sample.Services.DummyApi` for the pattern):

```csharp
public class MyExternalApi : ApiClient {
    public MyExternalApi() : base("https://api.example.com/v1/") { }
    protected override void SetDefaultRequestHeaders(HttpRequestHeaders headers) {
        headers.Add("X-Client", "MyApp");
    }
    public Task<Widget?> GetWidget(string id) => Get<Widget>($"widgets/{id}");
}
```
- Construct with `useAccessToken: true` and override `UpdateAccessToken`/`SetDefaultRequestHeaders` for APIs needing OAuth client-credentials/bearer refresh — the base class caches the token and refreshes it automatically once expired (`_accessExpire`).
- `Get<T>`/`Post<T>`/`Put<T>`/`Patch<T>`/`Delete`/`Download` all serialize/deserialize with camelCase JSON by default (override via the `jsonOptions` constructor callback), throw `ApiClientException` (carries the `HttpStatusCode`) on failure, and call the overridable `OnRequest(method, url, reqBody, respBody, success, elapsedMs)` hook — override it to pipe request/response logging into your own telemetry.
- `Get<T>` returns `null` on 404 rather than throwing.
- Set `EnsureSuccessStatusCode = false` in a subclass if you need to inspect non-2xx responses instead of getting an exception.
- `ClientSideRateLimiter`/`ClientSideRateLimitedHandler` (in the same namespace) provide an optional `DelegatingHandler` for client-side outbound rate limiting — wire it in if the remote API enforces strict rate limits and you want to smooth bursts instead of hitting 429s.

## Logging & Health

- `.WithLogging(opt => ...)` wires Application Insights (from `APPLICATIONINSIGHTS_CONNECTION_STRING` or `AppInsights:ConnectionString`), with knobs for dependency tracking, request/content tracking (`AppInsightsEnrichMiddleware`), JSON console logging, Azure Web App file diagnostics, and category-prefix log filtering. Health-check requests are filtered out of `Microsoft.AspNetCore.Hosting.Diagnostics` logs automatically to reduce noise.
- `.WithHealthCheck(path: "/health")` adds a health endpoint. `HealthService` aggregates any registered `IHealthStatus` (e.g. every `ServiceBusTrigger<T>` self-registers) — the app is "Unhealthy" if any background trigger reports `IsRunning == false`. Register your own `IHealthStatus` implementations (e.g. wrapping a change-feed processor) with `HealthService.AddCheck(this)` in their constructor to include them.

## Configuration key reference

| Section | Purpose |
|---|---|
| `CosmosDB:ConnectString` / `:DatabaseName` / `:DirectMode` | `AddDocumentDB` / also reused by `AddJobs` when `UseSqlServer=false` |
| `ServiceBus:ConnectString` / `:DeveloperMode` / `:MaxDeliveryCount` / `:IsBasicTier` | `AddServiceBus` |
| `Storage:{Name}:ConnectString` / `:CDN` | `AddBlobService(name)` |
| `Auth0:Domain` / `:ClientId` / `:ClientSecret` / `:Audience` / `:ApiIdentifier` | `WithAuth0` |
| `AppInsights:ConnectionString` or env `APPLICATIONINSIGHTS_CONNECTION_STRING` | `WithLogging` |
| `AppConfig` (in local appsettings, points at an Azure App Configuration connection string) | picked up by `UseInfra` to layer in centralized config |
| `ConnectionStrings:ConnectionString` | typical key used for Hangfire's SQL Server storage when `UseSqlServer=true` |

## Practical checklist when adding a feature in a consuming project

- **Need to persist documents?** Add a repo via `AddDocumentDB(bld => bld.AddRepo<I,T>())`, inject `IDocumentDB`, call `db.GetRepo<TDoc>(container, partitionKeyPath)`. Always supply the partition key on reads/writes when you have it.
- **Need async processing / decoupling?** Prefer the `Messages` layer (`MessageBase` + `MessageService<T>` + `MessageHandler<T>` + `AddMessageHandler<T,H>`) over raw `ServiceBusCommand`/`IServiceBusHandler`.
- **Need a recurring/background task?** Use `Jobs` (`JobRunner` + `AddCronJob`/`AddJob`), not a custom `IHostedService` with `Task.Delay` loops — you get the dashboard, retries-on-restart, and distributed queueing for free.
- **Need to store files?** `AddBlobService(name)` + `IBlobService`, not a raw `BlobServiceClient`.
- **Need to call another internal/external API?** Subclass `ApiClient`, don't hand-roll `HttpClient` + `JsonSerializer` boilerplate.
- **Local dev against shared Azure resources?** Turn on `ServiceBus:DeveloperMode = true` so your machine gets its own queue/subscription instead of racing other developers for messages.
