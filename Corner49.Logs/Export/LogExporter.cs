using Corner49.Logs.Configuration;
using Corner49.Logs.Domain;
using Corner49.Logs.Export.Database;
using Corner49.Logs.Parsing;
using Corner49.Logs.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;

namespace Corner49.Logs.Export;

/// <summary>
/// Orchestrates, per container: discover the sites (walking SUBSCRIPTIONS/RESOURCEGROUPS/SITES
/// one directory level at a time) -&gt; decide which UTC days to check per site -&gt; list each
/// day's blobs -&gt; for each blob, check its environment's database for that month (skip if
/// exported with the same ETag) -&gt; stream lines -&gt; parse -&gt; insert the events and the blob's
/// bookkeeping row in one transaction. One blob failing does not stop the others, and every blob
/// commits on its own, so a crash only loses the in-flight blob. When every container is done, the
/// monthly databases older than Export:RetentionMonths are deleted.
///
/// Which days get checked for a site in a container ("{container}/{service}"):
/// - an explicit Since: every day from Since through Until (default: today);
/// - never exported before (no day check of its environment's database mentions it): the last
///   Export:MaxAgeDays days through today;
/// - otherwise (incremental): every day that already has blobs in the environment's database,
///   plus every day from the newest one through today. A day that was already listed after the
///   day ended, with no failed blobs, is complete and not listed again - any other day is
///   listed, and whatever is missing, changed or failed is fetched from Azure.
/// "The environment's database" is all of its monthly databases together. In every case, days older
/// than Export:MaxAgeDays before today, or in months past Export:RetentionMonths, are never exported.
/// </summary>
public sealed class LogExporter(
		ILogStorageReader storageReader,
		LogParserDispatcher parserDispatcher,
		LogDatabaseFactory databases,
		ResolvedOutputDirectory outputDirectory,
		TimeProvider timeProvider,
		IOptions<ExportOptions> exportOptions,
		ILogger<LogExporter> logger) {
	private const int LinesPerProgressLog = 5000;

	/// <summary>
	/// Azure keeps appending to an hour's PT1H.json until shortly after that hour ends, so a
	/// day only counts as complete when it was listed at least this long after it ended.
	/// </summary>
	private static readonly TimeSpan SettleDelay = TimeSpan.FromHours(2);

	public async Task<ExportResult> ExportAsync(ExportRequest request, CancellationToken cancellationToken) {
		if (Directory.Exists(Path.Combine(outputDirectory.Path, "events")) || Directory.Exists(Path.Combine(outputDirectory.Path, "summaries"))) {
			logger.LogWarning(
					"Ignoring the old file-based export in {Directory}: events are now stored in one SQLite database per environment. " +
					"The events/ and summaries/ folders can be deleted.",
					outputDirectory.Path);
		}

		if (Directory.EnumerateFiles(outputDirectory.Path, "*.db").Any()) {
			logger.LogWarning(
					"Ignoring the single-file databases in {Directory}: events are now stored in one SQLite database per environment and month, " +
					"in a folder per environment. The *.db files directly in {Directory} can be deleted.",
					outputDirectory.Path, outputDirectory.Path);
		}

		DateTimeOffset startedAt = timeProvider.GetUtcNow();
		DateOnly today = DateOnly.FromDateTime(startedAt.UtcDateTime);
		int maxAgeDays = exportOptions.Value.MaxAgeDays;
		int retentionMonths = exportOptions.Value.RetentionMonths;
		DateOnly retentionCutoff = LogDatabaseFactory.MonthOf(today).AddMonths(-retentionMonths);
		DateOnly maxAgeDate = today.AddDays(-maxAgeDays);
		ExportRun run = new(startedAt, today, maxAgeDate > retentionCutoff ? maxAgeDate : retentionCutoff);

		if (request.Since < run.OldestDate) {
			logger.LogWarning(
					"--since {Since} is more than {MaxAgeDays} days ago (Export:MaxAgeDays) or before the {RetentionMonths} months kept " +
					"(Export:RetentionMonths); exporting from {OldestDate} instead.",
					request.Since, maxAgeDays, retentionMonths, run.OldestDate);
		}

		foreach (string container in request.Containers) {
			logger.LogInformation("Discovering sites in {Container}...", container);

			await foreach (SiteLocation site in DiscoverSitesAsync(container, request, cancellationToken)) {
				await ExportSiteAsync(run, container, site, request, cancellationToken);
			}
		}

		databases.DeleteExpired(retentionCutoff);

		logger.LogInformation("Export completed. Total events exported: {Total}", run.EventsExported);

		return new ExportResult(
				run.FilesFound, run.FilesImported, run.FilesSkipped, run.FilesFailed,
				run.EventsExported, run.LinesParsed, run.LinesUnparsed, run.DaysAlreadyComplete);
	}

	private async Task ExportSiteAsync(
			ExportRun run, string container, SiteLocation site, ExportRequest request, CancellationToken cancellationToken) {
		string checkKey = CheckKey(container, site.Service);
		EnvironmentIndex index = GetEnvironmentIndex(run, site.Environment);

		IEnumerable<DateOnly> dates;
		if (request.Since is { } since) {
			dates = DateRange(since, request.Until ?? run.Today);
		} else if (!index.CheckedKeys.Contains(checkKey)) {
			logger.LogInformation(
					"{Service} in {Container} has not been exported before; exporting from {OldestDate}.",
					site.Service, container, run.OldestDate);
			dates = DateRange(run.OldestDate, run.Today);
		} else {
			dates = index.Dates.Union(DateRange(index.Dates[^1], run.Today)).Order();
		}

		foreach (DateOnly date in dates.Where(d => IsInRange(run, request, d))) {
			cancellationToken.ThrowIfCancellationRequested();

			if (request.Since is null && index.CompleteKeys.TryGetValue(date, out HashSet<string>? complete) && complete.Contains(checkKey)) {
				logger.LogDebug("{Service} in {Container} on {Date} is already complete.", site.Service, container, date);
				run.DaysAlreadyComplete++;
				continue;
			}

			logger.LogInformation("Checking {Service} in {Container} on {Date}...", site.Service, container, date);

			DateTimeOffset listedAt = timeProvider.GetUtcNow();
			await ExportListingAsync(run, container, DiagnosticBlobPath.BuildDatePrefix(site.Prefix, date), cancellationToken);
			databases.Get(site.Environment, date).MarkChecked(date, checkKey, listedAt);
		}
	}

	/// <summary>Exports every blob under <paramref name="prefix"/> (one site's day).</summary>
	private async Task ExportListingAsync(ExportRun run, string container, string prefix, CancellationToken cancellationToken) {
		await foreach (LogFileInfo file in storageReader.ListFilesAsync(container, prefix, cancellationToken)) {
			cancellationToken.ThrowIfCancellationRequested();

			if (!BlobPathSegments.TryParse(file.Name, out BlobPathSegments? segments) || segments is null) {
				logger.LogWarning("Skipping {Blob}: could not determine environment/service/date/hour from its name.", file.Name);
				continue;
			}

			run.FilesFound++;

			await ExportFileAsync(run, container, file, segments, cancellationToken);
		}
	}

	private async Task ExportFileAsync(
			ExportRun run, string container, LogFileInfo file, BlobPathSegments segments, CancellationToken cancellationToken) {
		string blobKey = $"{container}/{file.Name}";
		LogDatabase database = databases.Get(segments.Environment, segments.Date);

		if (database.GetBlob(container, file.Name) is { Status: BlobStatus.Success, ETag: not null } existing &&
				existing.ETag == file.ETag) {
			logger.LogInformation("Skipping {Blob} (unchanged).", blobKey);
			run.FilesSkipped++;
			return;
		}

		logger.LogInformation("Exporting: {Blob}", blobKey);

		try {
			(long linesProcessed, long eventsExported, long linesUnparsed) =
					await ExportBlobAsync(database, container, file, blobKey, segments, cancellationToken);

			run.FilesImported++;
			run.EventsExported += eventsExported;
			run.LinesParsed += linesProcessed;
			run.LinesUnparsed += linesUnparsed;

			logger.LogInformation(
					"Lines processed: {LinesProcessed}, events exported: {EventsExported}, unparsed lines: {LinesUnparsed}",
					linesProcessed, eventsExported, linesUnparsed);
		} catch (OperationCanceledException) {
			throw;
		} catch (Exception ex) {
			logger.LogError(ex, "Failed to export {Blob}", blobKey);
			run.FilesFailed++;

			database.RecordFailure(container, file, segments, ex.Message);
		}
	}

	private async Task<(long LinesProcessed, long EventsExported, long LinesUnparsed)> ExportBlobAsync(
			LogDatabase database, string container, LogFileInfo file, string blobKey, BlobPathSegments segments, CancellationToken cancellationToken) {
		long lineNumber = 0;
		long eventsExported = 0;
		long linesUnparsed = 0;

		await using Stream blobStream = await storageReader.OpenReadAsync(container, file.Name, cancellationToken);
		using StreamReader reader = new(blobStream);
		using BlobImport import = database.BeginBlobImport(container, file, segments);

		string? line;
		while ((line = await reader.ReadLineAsync(cancellationToken)) is not null) {
			lineNumber++;

			if (string.IsNullOrWhiteSpace(line)) {
				continue;
			}

			LogEvent parsedEvent = parserDispatcher.Parse(line, blobKey, lineNumber);
			if (parsedEvent.EventType == LogEventType.Unparsed.ToString()) {
				linesUnparsed++;
			}

			import.Add(parsedEvent);
			eventsExported++;

			if (lineNumber % LinesPerProgressLog == 0) {
				logger.LogInformation("Lines processed: {LineNumber}", lineNumber);
			}
		}

		import.Complete(eventsExported, linesUnparsed);

		return (lineNumber, eventsExported, linesUnparsed);
	}

	/// <summary>
	/// Reads an environment's bookkeeping once per run, keeping only what's needed to plan
	/// which days to check.
	/// </summary>
	private EnvironmentIndex GetEnvironmentIndex(ExportRun run, string environment) {
		if (run.Environments.TryGetValue(environment, out EnvironmentIndex? cached)) {
			return cached;
		}

		DatabaseIndex stored = databases.LoadIndex(environment);

		HashSet<string> checkedKeys = stored.Checks.Select(c => c.CheckKey).ToHashSet();
		Dictionary<DateOnly, HashSet<string>> completeKeys = stored.Checks
				.Where(c => c.CheckedAt >= SettledAt(c.Date) && !stored.FailedKeys.Contains((c.Date, c.CheckKey)))
				.GroupBy(c => c.Date)
				.ToDictionary(g => g.Key, g => g.Select(c => c.CheckKey).ToHashSet());

		EnvironmentIndex index = new(stored.Dates, checkedKeys, completeKeys);
		run.Environments[environment] = index;
		return index;
	}

	private static DateTimeOffset SettledAt(DateOnly date) =>
			new DateTimeOffset(date.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) + SettleDelay;

	/// <summary>
	/// Walks resourceId=/SUBSCRIPTIONS/{sub}/RESOURCEGROUPS/{rg}/PROVIDERS/MICROSOFT.WEB/SITES/{site}/
	/// one directory level at a time, listing a level only when the request doesn't pin it.
	/// </summary>
	private async IAsyncEnumerable<SiteLocation> DiscoverSitesAsync(
			string container, ExportRequest request, [EnumeratorCancellation] CancellationToken cancellationToken) {
		await foreach (string subscription in ChildrenAsync(container, DiagnosticBlobPath.BuildPrefix(null, null, null), request.SubscriptionId, cancellationToken)) {
			await foreach (string resourceGroup in ChildrenAsync(container, DiagnosticBlobPath.BuildPrefix(subscription, null, null), request.ResourceGroup, cancellationToken)) {
				await foreach (string site in ChildrenAsync(container, DiagnosticBlobPath.BuildPrefix(subscription, resourceGroup, null), request.SiteName, cancellationToken)) {
					yield return new SiteLocation(resourceGroup, site, DiagnosticBlobPath.BuildPrefix(subscription, resourceGroup, site));
				}
			}
		}
	}

	private async IAsyncEnumerable<string> ChildrenAsync(
			string container, string prefix, string? filter, [EnumeratorCancellation] CancellationToken cancellationToken) {
		if (!string.IsNullOrWhiteSpace(filter)) {
			yield return filter.ToUpperInvariant();
			yield break;
		}

		await foreach (string childPrefix in storageReader.ListPrefixesAsync(container, prefix, cancellationToken)) {
			yield return childPrefix[prefix.Length..].TrimEnd('/').ToUpperInvariant();
		}
	}

	private static bool IsInRange(ExportRun run, ExportRequest request, DateOnly date) =>
			date >= run.OldestDate &&
			(request.Since is null || date >= request.Since) &&
			(request.Until is null || date <= request.Until);

	private static IEnumerable<DateOnly> DateRange(DateOnly from, DateOnly through) {
		for (DateOnly date = from; date <= through; date = date.AddDays(1)) {
			yield return date;
		}
	}

	private static string CheckKey(string container, string service) => $"{container}/{service}";

	/// <summary>A site found in storage; Environment is its resource group, Prefix its full blob-name prefix.</summary>
	private sealed record SiteLocation(string Environment, string Service, string Prefix);

	/// <summary>
	/// What planning needs from an environment's database: the days with blobs (oldest first),
	/// every "{container}/{service}" ever checked, and per day which ones are complete.
	/// </summary>
	private sealed record EnvironmentIndex(
			IReadOnlyList<DateOnly> Dates,
			HashSet<string> CheckedKeys,
			Dictionary<DateOnly, HashSet<string>> CompleteKeys);

	/// <summary>Per-run state: counters and the loaded environment indexes.</summary>
	private sealed class ExportRun(DateTimeOffset startedAt, DateOnly today, DateOnly oldestDate) {
		public DateTimeOffset StartedAt { get; } = startedAt;

		/// <summary>The run's current UTC day.</summary>
		public DateOnly Today { get; } = today;

		/// <summary>
		/// The oldest UTC day this run may export: Today minus Export:MaxAgeDays, but never before the
		/// first month kept by Export:RetentionMonths.
		/// </summary>
		public DateOnly OldestDate { get; } = oldestDate;

		public Dictionary<string, EnvironmentIndex> Environments { get; } = [];

		public int FilesFound;
		public int FilesImported;
		public int FilesSkipped;
		public int FilesFailed;
		public int DaysAlreadyComplete;
		public long EventsExported;
		public long LinesParsed;
		public long LinesUnparsed;
	}
}
