# Corner49.Infra.AzureCosmosDB

Opinionated repository layer for Azure Cosmos DB (NoSQL API). Part of Corner49.Infra – a set of packages that provide opinionated tooling for infrastructure in .NET applications.

- One `DocumentRepo<T>` per container, with point reads, LINQ and SQL queries, paging, streaming, patching, bulk operations and change feed
- Databases and containers are created at startup when they do not exist
- One shared `CosmosClient` per connection string
- `System.Text.Json` serialisation with camelCase names
- Built-in retry on `429` / `408`, and RU pressure monitoring for heavy jobs

```
dotnet add package Corner49.Infra.AzureCosmosDB
```

Already included when you install [Corner49.Infra](https://www.nuget.org/packages/Corner49.Infra).

## Configuration

```json
{
  "CosmosDB": {
    "ConnectString": "AccountEndpoint=https://...;AccountKey=...;",
    "DatabaseName": "data",
    "DirectMode": false
  }
}
```

| Key | Default | Meaning |
|---|---|---|
| `ConnectString` | – | Cosmos DB account connection string |
| `DatabaseName` | `data` | Default database for repos that do not name one |
| `DirectMode` | `false` | `true` uses direct (TCP) connections, `false` uses gateway mode |

## Getting started

### 1. Define a repository

```csharp
using Corner49.Infra.DB;

public interface IOrderRepo {
    Task<Order?> Get(string customerId, string id);
    Task<QueryResult<Order>> Open(string customerId, string? continuationToken);
    Task Save(Order order);
}

public class OrderRepo : IOrderRepo, IDocumentRepoInitializer {
    private readonly DocumentRepo<Order> _repo;

    public OrderRepo(IDocumentDB db) {
        // container name, then the partition key path(s)
        _repo = db.GetRepo<Order>("Orders", "customerId");
    }

    Task IDocumentRepoInitializer.Init() => _repo.Init();

    public Task<Order?> Get(string customerId, string id) => _repo.GetItem(customerId, id);

    public Task<QueryResult<Order>> Open(string customerId, string? continuationToken) =>
        _repo.Query(customerId, q => q.Where(o => o.Status == "open"), continuationToken, maxItemCount: 50);

    public Task Save(Order order) => _repo.UpsertItem(order.CustomerId, order);
}
```

`IDocumentRepoInitializer.Init()` is called for every registered repo at startup; `DocumentRepo<T>.Init()` creates the database and container when they are missing.

### 2. Register it

With an infra builder (Corner49.Infra or Corner49.Infra.CLI):

```csharp
infra.AddDocumentDB(db => {
    db.Configure = cfg => cfg.DatabaseName = "shop";   // optional override of the configuration
    db.AddRepo<IOrderRepo, OrderRepo>();
});
```

With a plain `IServiceCollection`:

```csharp
var db = services.AddDocumentDB(configuration, b => b.AddRepo<IOrderRepo, OrderRepo>());
// after the host is built:
await db.Init(app.Services);
```

Or without dependency injection:

```csharp
var db = new DocumentDB(connectString, dbName: "shop");
var repo = db.GetRepo<Order>("Orders", "customerId");
await repo.Init();
```

Repos are registered as singletons. Always go through `IDocumentDB` rather than creating your own `CosmosClient`.

## Partition keys

`GetRepo<T>(container, params string[] partitionKey)` takes one path for a simple key or several for a hierarchical key. Every operation has a `string` overload for the first case and a `string[]` overload for the second:

```csharp
_repo = db.GetRepo<Order>("Orders", "tenantId", "customerId");
await _repo.GetItem(new[] { tenantId, customerId }, id);
```

Pass `null` as the partition key to query across partitions. That is a fan-out query: use it deliberately.

## `DocumentRepo<T>` API

| Method | Use for |
|---|---|
| `GetItem` / `ReadItem` | Point read by partition key and id. Returns `null` when not found. The cheapest operation – prefer it whenever you know the id. |
| `AddItem` | Insert; fails when the id exists. |
| `UpsertItem` | Insert or replace. The optional `status` callback reports created versus replaced. |
| `PatchItem` | Partial update with a list of `PatchOperation`. |
| `DeleteItem` | Delete. Returns `false` when not found. |
| `Query(pk, linq, continuationToken?, maxItemCount?)` | LINQ query with paging. Returns `QueryResult<T>` (`Data`, `TotalCount`, `ContinuationToken`). |
| `Query(pk, sql, continuationToken?, maxItemCount?, parameters?)` | Parameterised SQL query with paging. |
| `Filter(pk, DocumentFilter<T>)` | Paged query driven by a filter object (`Search`, `Take`, `ContinuationToken`, overridable `Build`). Handy for list endpoints. |
| `Read(pk, linq, onRead)` / `ReadSQL(pk, sql, onRead)` | Process results one by one through a callback; return `false` from the callback to stop. |
| `GetItems(pk)` | All documents of a partition as `IAsyncEnumerable<T>`. |
| `ExecSQL<M>(pk, sql, ...)` | SQL projected into another type, as `IAsyncEnumerable<M>`. |
| `RawSQL` / `StreamSQL` | Results as `JsonElement` or as raw streams, for dynamic shapes and exports. |
| `CountSQL(pk, where)` | `SELECT COUNT(1)` with an optional where clause. |
| `CreateQuery(pk?, maxItemCount?)` | A raw `IQueryable<T>` to build on; run it with `GetQueryResults`. |
| `BulkInsert` / `BulkUpdate` / `BulkDelete` | Bulk operations over `IEnumerable<T>` or `IAsyncEnumerable<T>`. Tune with `BulkMaxConcurrency`, observe failures with `OnBulkError`. |
| `GetChangeFeedProcessor` / `GetAllChangesFeedProcessor` | A change feed processor for the container (lease container defaults to `changeLeases`). |
| `Exists()` | Whether the container exists. |
| `GetThroughputStats()` / `WaitForCapacity()` | RU pressure monitoring, see below. |

Continuation tokens returned by `Query` and `Filter` are opaque strings: pass them back unchanged for the next page. Always use `@param` placeholders with the `parameters` dictionary instead of concatenating values into SQL.

## Throughput

By default `Init()` creates the database and container without explicit throughput. Set autoscale limits on the repo before it is initialised:

```csharp
_repo = db.GetRepo<Order>("Orders", "customerId");
_repo.ContainerAutoscaleThroughput = 4000;   // autoscale max RU/s
```

Point operations are retried on `429 TooManyRequests` and `408 RequestTimeout`, honouring the server's retry-after.

For RU-heavy loops, pause before you get throttled instead of retrying afterwards:

```csharp
foreach (var order in orders) {
    await _repo.WaitForCapacity();                // waits while usage is above 80% of provisioned RU/s
    await _repo.UpsertItem(order.CustomerId, order);
}
```

`GetThroughputStats()` returns the observed RU/s, the provisioned RU/s, their ratio and the number of recent `429`s over a rolling window. The numbers only cover requests made through that repo instance in the current process.

## Diagnostics

```csharp
_repo.OnDiagnostics = diag => {
    logger.LogInformation("{Method} {Ms} ms, {RU} RU", diag.Method, diag.ElapsedTime?.TotalMilliseconds, diag.TotalRequestCharge);
    return Task.CompletedTask;
};
```

## Errors

Failures surface as `DocumentException` (with a `StatusCode`). `DocumentContainerNotFoundException` means the container was used before `Init()` ran. A missing document is not an error: reads return `null` and deletes return `false`.

## Serialisation

Documents are serialised with `System.Text.Json` using `JsonHelper.Options` from Corner49.Infra.Core: camelCase property names, enums as strings, nulls omitted. Use `[JsonPropertyName]` only where the stored name must differ, and make sure your document exposes `id`.

## Related packages

- [Corner49.Infra](https://www.nuget.org/packages/Corner49.Infra) – web apps and APIs
- [Corner49.Infra.CLI](https://www.nuget.org/packages/Corner49.Infra.CLI) – console apps and jobs
- [Corner49.Infra.Core](https://www.nuget.org/packages/Corner49.Infra.Core) – abstractions and utilities

Source: https://github.com/FrankVDL2471/Corner49.Infra
