namespace Corner49.Logs.Storage;

/// <summary>
/// Abstraction over the blob storage the importer reads log files from. No Azure-specific
/// types leak past this boundary so tests can supply a fake/local implementation.
/// </summary>
public interface ILogStorageReader {
	/// <summary>
	/// Streams blob metadata one item at a time instead of buffering the whole listing, so a
	/// broad prefix (e.g. no site filter) doesn't hold every blob in the container/subscription
	/// in memory at once.
	/// </summary>
	IAsyncEnumerable<LogFileInfo> ListFilesAsync(
			string containerName, string? prefix, CancellationToken cancellationToken);

	/// <summary>
	/// Lists the "virtual directories" directly below <paramref name="prefix"/> (which must end
	/// in '/'), each returned as a full prefix ending in '/'. Used to discover the
	/// subscriptions/resource groups/sites present without listing every blob below them.
	/// </summary>
	IAsyncEnumerable<string> ListPrefixesAsync(
			string containerName, string prefix, CancellationToken cancellationToken);

	Task<Stream> OpenReadAsync(
			string containerName, string blobName, CancellationToken cancellationToken);
}
