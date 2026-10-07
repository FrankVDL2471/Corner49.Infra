# Corner49.Infra.AzureStorage

Opinionated wrappers for Azure Blob Storage and Azure File Shares. Part of Corner49.Infra – a set of packages that provide opinionated tooling for infrastructure in .NET applications.

- `IBlobService` – upload, download, append, list and move blobs, with containers created on first write
- Several storage accounts side by side, each registered under a name
- Optional CDN host, so uploads return the public URL
- `FileService` – read and write files on an Azure file share

```
dotnet add package Corner49.Infra.AzureStorage
```

Already included when you install [Corner49.Infra](https://www.nuget.org/packages/Corner49.Infra).

## Configuration

Each named blob service reads its own section:

```json
{
  "Storage": {
    "Documents": {
      "ConnectString": "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...",
      "CDN": "https://cdn.example.com"
    }
  }
}
```

| Key | Meaning |
|---|---|
| `Storage:{Name}:ConnectString` | Storage account connection string |
| `Storage:{Name}:CDN` | Optional. Host used to build the URLs returned by uploads |

## Getting started

Register a blob service by name, on an infra builder or on a plain `IServiceCollection`:

```csharp
infra.AddBlobService("Documents");
// or
services.AddBlobService("Documents");
```

It is registered as a keyed scoped service. Inject it with the key, or resolve it from a service provider:

```csharp
using Corner49.Infra.Storage;

public class InvoiceController : ControllerBase {
    private readonly IBlobService _blobs;

    public InvoiceController([FromKeyedServices("Documents")] IBlobService blobs) {
        _blobs = blobs;
    }

    [HttpPost]
    public async Task<string?> Upload(IFormFile file) {
        using var stream = file.OpenReadStream();
        return await _blobs.Upload("invoices", file.FileName, stream, file.ContentType);
    }
}
```

```csharp
var blobs = serviceProvider.GetBlobService("Documents");
```

Without dependency injection:

```csharp
var blobs = new BlobService(connectString);
```

## `IBlobService` API

Every method takes a container name and a blob name.

| Method | Purpose |
|---|---|
| `Upload(container, name, Stream \| byte[], contentType?, metaData?)` | Uploads a blob, replacing an existing one. Returns its URL. |
| `UploadBase64(container, name, data, contentType?)` | Same, from a base64 string. |
| `Write(container, name, contentType?, metaData?)` | Opens a stream to write a blob. |
| `Read(container, name)` / `DownloadFile(container, name)` | Opens a blob as a stream; `null` when it does not exist. |
| `GetFile(container, name, target)` | Copies a blob into a stream. |
| `GetBlob(container, name, target)` | Same, and returns `BlobInfo` (`ContentType`, `ETag`, `ContentLength`, `Date`). |
| `GetFileInBase64(container, name)` | Returns the content as base64. |
| `Exists(container, name)` | Whether the blob exists. |
| `SetMeta(container, name, contentType?, metaData?)` | Updates content type and metadata. |
| `Delete(container, name)` | Deletes a blob. |
| `MoveFile(sourceContainer, sourceName, targetContainer, targetName)` | Copies, then deletes the source. |
| `GetFiles(container)` | All blob names in a container. |
| `GetItems(container, path?)` | Hierarchical listing: blob names and `prefix/` entries for virtual folders. |
| `CreateText` / `AppendText` / `Append` | Append blobs for logs and incremental writes. `AppendText` rolls over to `name-part1`, `name-part2`, ... when a blob is full. |
| `GetCDN(container, name)` | Builds `{CDN}/{container}/{name}`. |

## Behaviour to be aware of

- **Containers are created on first write, with public read access for blobs.** Anyone with the URL can read an uploaded blob. Do not use this for private data without changing the container's access level.
- **Uploads overwrite.** An existing blob with the same name is deleted first.
- **Returned URLs depend on the `CDN` setting.** Without it the returned value is only the relative `/{container}/{name}` path.
- **Container names are normalised** to satisfy Azure's naming rules (underscores removed, upper-case converted). Use lower-case names to get exactly the container you asked for.

## File shares

```csharp
using Corner49.Infra.Storage;

var files = new FileService(connectString, "exports");   // creates the share when missing

await files.Updload("2026/10", "orders.csv", stream);
using Stream? content = await files.Read("2026/10", "orders.csv");
bool found = await files.Download("2026/10", "orders.csv", target);
```

Directories in the path are created as needed. `Write(path, fileName, size)` opens a stream for a file of a known size.

## Related packages

- [Corner49.Infra](https://www.nuget.org/packages/Corner49.Infra) – web apps and APIs
- [Corner49.Infra.CLI](https://www.nuget.org/packages/Corner49.Infra.CLI) – console apps and jobs
- [Corner49.Infra.Core](https://www.nuget.org/packages/Corner49.Infra.Core) – abstractions and utilities

Source: https://github.com/FrankVDL2471/Corner49.Infra
