# Corner49.Infra

Corner49.Infra is a set of NuGet packages that provide **opinionated tooling for infrastructure** in .NET 10 applications. Instead of wiring up configuration, logging, authentication, Cosmos DB, Service Bus and Blob Storage by hand in every project, you start from one fluent builder and opt into the pieces you need.

```csharp
using Corner49.Infra;

var infra = WebApplication.CreateBuilder(args)
    .UseInfra("MyApp")
    .WithApiControllers()
    .WithLogging()
    .WithHealthCheck();

infra.AddDocumentDB(db => db.AddRepo<IOrderRepo, OrderRepo>());

await infra.BuildAndRun();
```

## Packages

| Package | Use it for |
|---|---|
| [Corner49.Infra](Corner49.Infra/README.md) | ASP.NET Core web apps and APIs: controllers, OpenAPI, Auth0, CORS, health checks, Hangfire jobs, SignalR, Application Insights. References all packages below except the CLI. |
| [Corner49.Infra.CLI](Corner49.Infra.CLI/README.md) | Console apps and containerised jobs: the same bootstrap for a generic host, with named jobs or `System.CommandLine`. |
| [Corner49.Infra.AzureCosmosDB](Corner49.Infra.AzureCosmosDB/README.md) | Azure Cosmos DB repositories (`DocumentRepo<T>`) with automatic container creation, paging, bulk operations and RU monitoring. |
| [Corner49.Infra.AzureServiceBus](Corner49.Infra.AzureServiceBus/README.md) | Azure Service Bus queues and topics: auto-provisioning, typed messages, hosted handlers. |
| [Corner49.Infra.AzureStorage](Corner49.Infra.AzureStorage/README.md) | Azure Blob Storage (`IBlobService`) and Azure File Shares (`FileService`). |
| [Corner49.Infra.Core](Corner49.Infra.Core/README.md) | Shared abstractions (`IInfraBuilder`, `InfraExtension`) and small utilities. Pulled in by every other package. |

All packages target `net10.0` and share the `Corner49.Infra` root namespace.

### Which package do I install?

- **Web app or API** – install `Corner49.Infra`. Cosmos DB, Service Bus and Storage come along as dependencies.
- **Console app, worker or scheduled container job** – install `Corner49.Infra.CLI` plus the Azure packages you need.
- **Only one Azure service, no builder** – install that package on its own; each one also works with a plain `IServiceCollection` or by constructing the service directly.

`Corner49.Infra` and `Corner49.Infra.CLI` both define `Corner49.Infra.InfraBuilder`, so reference only one of them per project.

## How it fits together

Every app starts with `UseInfra(appName)`, which returns an `InfraBuilder`:

1. **Configuration** is loaded from `appsettings.json`, `appsettings.{Environment}.json`, `appsettings.{MachineName}.json` and environment variables. When that local configuration contains an `AppConfig` connection string, Azure App Configuration is layered in underneath it, so local files always win.
2. **Features** are switched on with `With...()` methods on the builder (logging, controllers, auth, health checks).
3. **Azure services** plug in as extensions (`AddDocumentDB`, `AddServiceBus`, `AddBlobService`). These are extension methods on `IInfraBuilder`, so call them on the `infra` variable after the `With...()` chain.
4. **`BuildAndRun()`** builds the host, lets each extension initialise itself (for example creating missing Cosmos DB containers) and runs the app.

You can write your own extension by deriving from `InfraExtension` – see [Corner49.Infra.Core](Corner49.Infra.Core/README.md).

## Configuration keys

| Key | Used by |
|---|---|
| `AppConfig` | `UseInfra` – Azure App Configuration connection string (optional) |
| `CosmosDB:ConnectString`, `CosmosDB:DatabaseName`, `CosmosDB:DirectMode` | `AddDocumentDB` |
| `ServiceBus:ConnectString`, `ServiceBus:DeveloperMode`, `ServiceBus:MaxDeliveryCount`, `ServiceBus:IsBasicTier` | `AddServiceBus` |
| `Storage:{Name}:ConnectString`, `Storage:{Name}:CDN` | `AddBlobService(name)` |
| `Auth0:Domain`, `Auth0:ClientId`, `Auth0:ClientSecret`, `Auth0:Audience`, `Auth0:ApiIdentifier` | `WithAuth0` |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` or `AppInsights:ConnectionString` | `WithLogging` |

## Repository layout

| Folder | Contents |
|---|---|
| `Corner49.Infra*` | The packages listed above |
| `Corner49.FormBuilder` | Dynamic form builder for MVC apps (separate package) |
| `Corner49.LogViewer` | Viewer for application logs stored in a blob container (separate package) |
| `Corner49.Sample` | MVC sample app |
| `Corner49.SampleAPI` | API sample app |
| `Corner49.SampleCLI` | Console sample app with jobs |

## Building and publishing

```
dotnet build
dotnet pack --configuration Release --output ./nupkg
```

Every push to `main` or a `release/*` branch runs [publish-nuget.yml](.github/workflows/publish-nuget.yml), which packs all packages as version `10.0.{run number}` and pushes them to nuget.org. Each package ships its own `README.md`, which is what nuget.org shows on the package page.
