# Corner49.Infra.CLI

Opinionated bootstrap for .NET console apps, workers and containerised jobs. Part of Corner49.Infra – a set of packages that provide opinionated tooling for infrastructure in .NET applications.

It gives a generic host the same configuration, logging and extension model that [Corner49.Infra](https://www.nuget.org/packages/Corner49.Infra) gives a web app, and adds two ways to decide what the process does: named jobs or `System.CommandLine`.

```
dotnet add package Corner49.Infra.CLI
```

Do not reference this package together with `Corner49.Infra` in the same project; both define `Corner49.Infra.InfraBuilder`.

## Getting started

```csharp
using Corner49.Infra;

var infra = InfraBuilder.Create("MyTool", args)
    .WithLogging(opt => opt.WriteToConsoleAsJson = false);

infra.AddJob<HelloWorldJob>("Hello", "Print hello world in the console");
infra.AddJob<ImportJob>("Import", "Import the nightly product feed");

await infra.BuildAndRun(args);
```

```csharp
using Corner49.Infra.Jobs;

public class ImportJob : IJob {
    private readonly IProductRepo _repo;

    public ImportJob(IProductRepo repo) {
        _repo = repo;
    }

    public async Task Execute(string[]? args = null, CancellationToken cancellationToken = default) {
        // args[0] is the job name, the rest are your own arguments
    }
}
```

Run a job by passing its name as the first argument (not case sensitive):

```
MyTool import 2026-10-07
```

Running without arguments prints the registered jobs and their descriptions.

## What `BuildAndRun(args)` does

After building the host and starting the registered extensions, it picks one of three modes:

| Registered | Behaviour |
|---|---|
| `WithCommandLine(...)` | Parses `args` with your `System.CommandLine` root command and invokes it. Takes precedence over jobs. |
| One or more `AddJob<T>(...)` | Runs the job named by `args[0]`, then exits. |
| Neither | Runs the host until shutdown (`RunAsync`), for hosted services and workers. |

## Builder API

| Method | Purpose |
|---|---|
| `InfraBuilder.Create(name, args)` | Creates a `HostApplicationBuilder` and calls `UseInfra` on it. |
| `builder.UseInfra(name, environment?)` | Same, for an existing `IHostApplicationBuilder`. |
| `WithOptions<T>(section?)` | Binds a configuration section to `T`. The section name defaults to the type name without its `Options` or `Configuration` suffix. |
| `WithLogging(options?)` | Activity tracking, optional JSON console output and category-prefix filtering (errors always pass the filter). A plain console logger is added when a debugger is attached or `ConsoleLog=true` is set. |
| `AddJob<T>(name, description?)` | Registers an `IJob` as a keyed singleton under `name`. |
| `WithCommandLine(cmd => ..., description)` | Configures a `System.CommandLine` `RootCommand`. |
| `AddExtension(extension)` | Adds an `InfraExtension`; this is what `AddDocumentDB`, `AddServiceBus` and friends use. |
| `Services`, `Configuration` | The underlying `IServiceCollection` and `IConfigurationManager` for your own registrations. |

After the host is built, `InfraBuilder.Instance.Services` exposes the service provider for code that cannot use constructor injection.

## Configuration

`UseInfra` loads, in this order:

1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. `appsettings.{MachineName}.json`
4. Environment variables

All files are optional. If that configuration contains an `AppConfig` connection string, Azure App Configuration is added underneath it (unlabelled keys, then keys labelled with the environment name, then keys labelled with the machine name). Local settings always override App Configuration.

## Using the Azure packages

```csharp
infra.AddDocumentDB(db => db.AddRepo<IProductRepo, ProductRepo>());   // Corner49.Infra.AzureCosmosDB
infra.AddServiceBus(cfg => { });                                      // Corner49.Infra.AzureServiceBus
infra.AddBlobService("Documents");                                    // Corner49.Infra.AzureStorage
```

Repositories registered this way can be injected straight into your jobs.

## Related packages

- [Corner49.Infra.Core](https://www.nuget.org/packages/Corner49.Infra.Core) – abstractions and utilities
- [Corner49.Infra.AzureCosmosDB](https://www.nuget.org/packages/Corner49.Infra.AzureCosmosDB)
- [Corner49.Infra.AzureServiceBus](https://www.nuget.org/packages/Corner49.Infra.AzureServiceBus)
- [Corner49.Infra.AzureStorage](https://www.nuget.org/packages/Corner49.Infra.AzureStorage)

Source: https://github.com/FrankVDL2471/Corner49.Infra
