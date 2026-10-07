# Corner49.Infra.Core

Shared abstractions and utilities for the Corner49.Infra packages – a set of packages that provide opinionated tooling for infrastructure in .NET applications.

You normally do not install this package directly: it comes along with [Corner49.Infra](https://www.nuget.org/packages/Corner49.Infra), [Corner49.Infra.CLI](https://www.nuget.org/packages/Corner49.Infra.CLI) and the Azure packages. Install it on its own when you write your own infra extension or only want the utilities.

```
dotnet add package Corner49.Infra.Core
```

## Abstractions

| Type | Purpose |
|---|---|
| `IInfraBuilder` | The builder contract every extension targets: `Name`, `Services`, `Configuration` and `AddExtension(...)`. Implemented by `InfraBuilder` in Corner49.Infra and Corner49.Infra.CLI. |
| `InfraExtension` | Base class for a pluggable feature. `Build` runs before the host is built (register services here), `Start` runs after it is built (initialise resources here). |
| `LoggingOptions` | Options for `WithLogging(...)`: `TrackActivity`, `TrackDependencies`, `TrackContent`, `WriteToConsoleAsJson`, `FilterCategoryPrefix`. |

### Writing an extension

```csharp
using Corner49.Infra;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public class CacheExtension : InfraExtension {

    public override Task Build(IInfraBuilder infra, IConfiguration config) {
        infra.Services.Configure<CacheOptions>(config.GetSection("Cache"));
        infra.Services.AddSingleton<ICache, RedisCache>();
        return Task.CompletedTask;
    }

    public override Task Start(IServiceProvider serviceProvider) {
        return serviceProvider.GetRequiredService<ICache>().Connect();
    }
}

public static class CacheServiceExtensions {
    public static IInfraBuilder AddCache(this IInfraBuilder infra) {
        return infra.AddExtension(new CacheExtension());
    }
}
```

Because the extension only depends on `IInfraBuilder`, it works in both web apps and console apps.

## Utilities

All in the `Corner49.Infra.Tools` namespace.

| Type | What it does |
|---|---|
| `JsonHelper` | `JsonHelper.Options` and `options.SetDefault()` – the `System.Text.Json` defaults used across the packages: camelCase names, enums as strings, nulls omitted on write, numbers readable from strings. |
| `Base36`, `Base62` | Compact ids: `NewId()` (from a GUID), `FromGuid` / `ToGuid`, `FromNumber` / `ToNumber`. `Base36.NewShortId()` combines a timestamp with a random number. |
| `Base64` | `Encode` / `Decode` for UTF-8 strings. |
| `Generator` | `NewId()` (short base36 id), `GetCRC(string)` and `Guid.ToShort()`. |
| `Hasher` | `GetEmailHash(email)` – a deterministic obfuscated key for an e-mail address. Not a cryptographic hash. |
| `DateTimeExtensions` | Time-zone aware conversion and formatting: `ConvertToUtc`, `ConvertToLocalTime`, `ToLocalString`, `FormatToString`, `FirstOfMonth`. The time zone defaults to `Europe/Brussels` when none is passed. |
| `DateTimeHelper` | `DateToInt` / `DateFromInt` – dates as `yyyyMMdd` integers. |

```csharp
using Corner49.Infra.Tools;

string id = Generator.NewId();
string json = JsonSerializer.Serialize(order, JsonHelper.Options);
DateTime utc = localTime.ConvertToUtc("Europe/Amsterdam");
int day = DateTime.Today.DateToInt();   // 20261007
```

## Related packages

- [Corner49.Infra](https://www.nuget.org/packages/Corner49.Infra) – web apps and APIs
- [Corner49.Infra.CLI](https://www.nuget.org/packages/Corner49.Infra.CLI) – console apps and jobs
- [Corner49.Infra.AzureCosmosDB](https://www.nuget.org/packages/Corner49.Infra.AzureCosmosDB)
- [Corner49.Infra.AzureServiceBus](https://www.nuget.org/packages/Corner49.Infra.AzureServiceBus)
- [Corner49.Infra.AzureStorage](https://www.nuget.org/packages/Corner49.Infra.AzureStorage)

Source: https://github.com/FrankVDL2471/Corner49.Infra
