using Azure.Storage.Blobs;
using Corner49.Logs.Configuration;
using Corner49.Logs.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;

namespace Corner49.Logs.Storage;

public sealed class AzureBlobLogStorageReader(
		IOptions<AzureStorageOptions> options,
		ILogger<AzureBlobLogStorageReader> logger) : ILogStorageReader {
	private BlobServiceClient? _client;

	public async IAsyncEnumerable<LogFileInfo> ListFilesAsync(
			string containerName, string? prefix, [EnumeratorCancellation] CancellationToken cancellationToken) {
		BlobContainerClient containerClient = GetClient().GetBlobContainerClient(containerName);

		await foreach (var blobItem in containerClient.GetBlobsAsync(
				Azure.Storage.Blobs.Models.BlobTraits.None,
				Azure.Storage.Blobs.Models.BlobStates.None,
				prefix,
				cancellationToken)) {
			yield return new LogFileInfo(
					blobItem.Name,
					blobItem.Properties.ContentLength ?? 0,
					blobItem.Properties.LastModified,
					blobItem.Properties.ETag?.ToString());
		}
	}

	public async IAsyncEnumerable<string> ListPrefixesAsync(
			string containerName, string prefix, [EnumeratorCancellation] CancellationToken cancellationToken) {
		BlobContainerClient containerClient = GetClient().GetBlobContainerClient(containerName);

		await foreach (var item in containerClient.GetBlobsByHierarchyAsync(
				Azure.Storage.Blobs.Models.BlobTraits.None,
				Azure.Storage.Blobs.Models.BlobStates.None,
				"/",
				prefix,
				cancellationToken)) {
			if (item.IsPrefix) {
				yield return item.Prefix;
			}
		}
	}

	public async Task<Stream> OpenReadAsync(string containerName, string blobName, CancellationToken cancellationToken) {
		BlobContainerClient containerClient = GetClient().GetBlobContainerClient(containerName);
		BlobClient blobClient = containerClient.GetBlobClient(blobName);
		return await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
	}

	private BlobServiceClient GetClient() {
		if (_client is not null) {
			return _client;
		}

		string connectionString = options.Value.ConnectionString
				?? throw new InvalidOperationException("No Azure Storage connection string configured.");

		logger.LogInformation(
				"Connecting to Azure Blob Storage account {AccountName}.",
				ConnectionStringRedactor.RedactAzureStorageConnectionString(connectionString));
		_client = new BlobServiceClient(connectionString);

		return _client;
	}
}
