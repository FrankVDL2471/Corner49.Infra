# Corner49.Infra

Opinionated bootstrap for ASP.NET Core web apps and APIs. The main package of Corner49.Infra – a set of packages that provide opinionated tooling for infrastructure in .NET applications.

One fluent builder replaces the usual `Program.cs` plumbing: configuration, logging, controllers, OpenAPI, authentication, CORS, health checks, background jobs and the request pipeline.

```
dotnet add package Corner49.Infra
```

This package brings in [Corner49.Infra.AzureCosmosDB](https://www.nuget.org/packages/Corner49.Infra.AzureCosmosDB), [Corner49.Infra.AzureServiceBus](https://www.nuget.org/packages/Corner49.Infra.AzureServiceBus), [Corner49.Infra.AzureStorage](https://www.nuget.org/packages/Corner49.Infra.AzureStorage) and [Corner49.Infra.Core](https://www.nuget.org/packages/Corner49.Infra.Core). For console apps use [Corner49.Infra.CLI](https://www.nuget.org/packages/Corner49.Infra.CLI) instead.

## Getting started

**API**

```csharp
using Corner49.Infra;

var infra = WebApplication.CreateBuilder(args)
    .UseInfra("Shop")
    .WithLogging()
    .WithHealthCheck()
    .WithApiControllers()
    .WithCors("https://shop.example.com")
    .WithAuth0();

infra.AddDocumentDB(db => db.AddRepo<IOrderRepo, OrderRepo>());
infra.AddServiceBus(cfg => { }, bus => bus.AddMessageHandler<OrderMessage, OrderMessageHandler>());
infra.AddBlobService("Documents");

infra.Services.AddSingleton<IOrderService, OrderService>();

await infra.BuildAndRun();
```

**MVC app**

```csharp
var infra = WebApplication.CreateBuilder(args)
    .UseInfra("Shop")
    .WithViewControllers()
    .WithLogging()
    .WithAuth0();

await infra.BuildAndRun();
```

`AddDocumentDB`, `AddServiceBus` and `AddBlobService` come from the Azure packages and extend `IInfraBuilder`, so call them on the `infra` variable rather than inside the `With...()` chain.

## Builder API

| Method | What it sets up |
|---|---|
| `UseInfra(appName, environment?)` | Configuration (see below) and the builder itself. Available on `WebApplicationBuilder` and `HostApplicationBuilder`. |
| `WithOptions<T>(section?)` | Binds a configuration section to `T`. The section name defaults to the type name without its `Options` or `Configuration` suffix. |
| `WithLogging(options?)` | Application Insights, activity tracking, console logging and category filtering. |
| `WithHealthCheck(path = "/health")` | A health endpoint. |
| `WithApiControllers(apiVersion = "v1", jsonOptions?, openApi?)` | API controllers, an OpenAPI document and the Scalar API reference UI. |
| `WithViewControllers(jsonOptions?, mvcBuilder?)` | MVC controllers with views, Razor runtime compilation, static files and the default route. |
| `WithCors(params origins)` / `WithCors(isAllowedOrigin)` | A CORS policy. Without origins, any origin is allowed. |
| `WithAuth0(options?)` | Auth0 authentication, see below. |
| `WithApiKey<T>()` | API-key protection for actions marked with `[ApiKey]`. |
| `WithErrorHandler(showDeveloperError, errorPage?)` | Error page behaviour for MVC apps outside Development. |
| `AddJobs(builder, config?)` | Hangfire background and recurring jobs. |
| `AddSession(options?)` | ASP.NET Core session state. |
| `AddSignalRHub(connectionString)` / `AddSignalRClient(hubName, connectionString)` | Azure SignalR as a hub host or as a server-side client. |
| `AddExtension(extension)` | Plugs in an `InfraExtension`. |
| `BuildAndRun(afterBuild?, map?, fallbackUrl?)` | Builds the app, configures the pipeline, starts extensions and runs. |

`infra.Services` and `infra.Configuration` give access to the underlying service collection and configuration. After startup, `InfraBuilder.Instance.Services` exposes the service provider to code that cannot use constructor injection.

### Order matters in two places

- Call `WithApiControllers()` / `WithViewControllers()` **before** `WithAuth0()`. Auth0 configures JWT bearer or cookie login depending on which controller types are registered.
- Call `WithHealthCheck()` **before** `WithApiControllers()` to include the built-in `HealthService` check.

## Configuration

`UseInfra` loads, in this order:

1. `appsettings.json` (required)
2. `appsettings.{Environment}.json`
3. `appsettings.{MachineName}.json`
4. Environment variables

If that configuration contains an `AppConfig` connection string, Azure App Configuration is added underneath it (unlabelled keys, then keys labelled with the environment name, then keys labelled with the machine name). Local settings always override App Configuration, so a developer's own file wins.

## Logging

```csharp
.WithLogging(opt => {
    opt.TrackDependencies = true;                       // Application Insights dependency tracking
    opt.TrackContent = true;                            // request enrichment middleware
    opt.WriteToConsoleAsJson = true;
    opt.FilterCategoryPrefix = new[] { "Shop", "Corner49" };
})
```

Logging is configured when an Application Insights connection string is present in `APPLICATIONINSIGHTS_CONNECTION_STRING` or `AppInsights:ConnectionString`. With `FilterCategoryPrefix` set, only categories starting with one of the prefixes are logged; errors and critical messages always pass.

## Authentication

**Auth0**

```json
{
  "Auth0": {
    "Domain": "example.eu.auth0.com",
    "Audience": "https://api.example.com",
    "ClientId": "...",
    "ClientSecret": "...",
    "ApiIdentifier": "..."
  }
}
```

- With API controllers: JWT bearer validation against the Auth0 domain. `Audience` is required.
- With view controllers: interactive login with cookies. `ClientId` is required.

Values can also be set in code: `.WithAuth0(opt => opt.Domain = "...")`. `Auth0Service` is a small client for looking up and deleting Auth0 users.

**API key**

```csharp
public class ApiKeyValidation : IApiKeyValidation {
    public bool IsValidApiKey(string userApiKey) => userApiKey == "...";
}

infra.WithApiKey<ApiKeyValidation>();
```

```csharp
[ApiKey]
[HttpPost("webhook")]
public IActionResult Webhook() => Ok();
```

The key is read from the `X-API-Key` header or the `apiKey` query parameter.

## Health checks

`WithHealthCheck()` maps the endpoint. Report your own background components by implementing `IHealthStatus` (`Name`, `IsRunning`) and calling `HealthService.AddCheck(this)`; the app reports unhealthy as soon as one of them is not running.

## Jobs

Background and recurring jobs run on Hangfire.

```csharp
using Corner49.Infra.Jobs;

public class CleanupJob : JobRunner {
    public CleanupJob(IServiceProvider serviceProvider) : base(serviceProvider) { }

    public override Task Execute(Dictionary<string, string>? args = null, CancellationToken cancellationToken = default) {
        // ...
        return Task.CompletedTask;
    }
}

infra.AddJobs(jobs => {
    jobs.AddCronJob<CleanupJob>(cron => cron.EveryMinute(15));
    jobs.AddJob<IExportJob, ExportJob>();
}, cfg => {
    cfg.UseSqlServer = true;
    cfg.ConnectString = infra.Configuration["ConnectionStrings:Jobs"];
    cfg.DbName = "jobs";
});
```

Start an on-demand job through `IJobManager.StartJob<T>(args, queue)`.

| `JobConfig` | Default | Meaning |
|---|---|---|
| `UseSqlServer` | `false` | Store jobs in SQL Server (`ConnectString`, `DbName`). Otherwise jobs are kept **in memory** and are lost on restart. |
| `RunServer` | `true` | Process jobs in this app. Set to `false` to only enqueue. |
| `QueueName` | `default` | Queue to process and schedule on. |
| `UseLocalQueue` | `false` | Use a queue named after the machine, so a developer only runs their own jobs. |
| `WorkerCount` | Hangfire default | Number of workers. |
| `EnableDashboard` | `true` | Hangfire dashboard on `/jobs`. |

Things to know:

- Failed jobs are **not retried**; the exception is logged. Add retry logic in `Execute` where needed.
- The dashboard on `/jobs` has **no authentication**. Disable it or shield it at the network level in production.

## Calling other APIs

Derive a typed client from `ApiClient`:

```csharp
using Corner49.Infra.Http;

public class CatalogApi : ApiClient {
    public CatalogApi() : base("https://api.example.com/v1/") { }

    protected override void SetDefaultRequestHeaders(HttpRequestHeaders headers) {
        headers.Add("X-Client", "Shop");
    }

    public Task<Product?> GetProduct(string id) => Get<Product>($"products/{id}");
}
```

`Get`, `Post`, `Put`, `Patch`, `Delete` and `Download` use camelCase JSON. `Get<T>` returns `null` on a 404; other failures throw `ApiClientException` with the status code. Pass `useAccessToken: true` and override `UpdateAccessToken` for APIs that need a bearer token, and override `OnRequest` to log calls.

## What `BuildAndRun` configures

HTTPS redirection and HSTS (outside Development), static files and routing for MVC apps, the OpenAPI document and Scalar UI for APIs, CORS, the Hangfire dashboard, authentication and authorization, session, the health endpoint and controller routes – in that order. Use the `afterBuild` and `map` callbacks to add your own middleware and endpoints:

```csharp
await infra.BuildAndRun(map: app => app.MapHub<OrderHub>("/hubs/orders"));
```

## Related packages

- [Corner49.Infra.CLI](https://www.nuget.org/packages/Corner49.Infra.CLI) – console apps and jobs
- [Corner49.Infra.AzureCosmosDB](https://www.nuget.org/packages/Corner49.Infra.AzureCosmosDB) – Cosmos DB repositories
- [Corner49.Infra.AzureServiceBus](https://www.nuget.org/packages/Corner49.Infra.AzureServiceBus) – Service Bus messaging
- [Corner49.Infra.AzureStorage](https://www.nuget.org/packages/Corner49.Infra.AzureStorage) – Blob Storage and File Shares
- [Corner49.Infra.Core](https://www.nuget.org/packages/Corner49.Infra.Core) – abstractions and utilities

Source: https://github.com/FrankVDL2471/Corner49.Infra
