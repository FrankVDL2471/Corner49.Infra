using Corner49.Logs.Export;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Corner49.Logs.Export.Database;

/// <summary>
/// Opens (creating on first use) one <see cref="LogDatabase"/> per environment and month, stored as
/// {data dir}/{environment}/{environment}-{yyyy-MM}.db with the resource group lowercased - e.g.
/// my-app-dev/my-app-dev-2026-10.db for October's logs of the MY-APP-DEV resource group - and keeps
/// it open for the rest of the run. A blob (with its events and its day's checks) lives in the
/// database of its blob path's month, so each day is in exactly one file.
/// </summary>
public sealed class LogDatabaseFactory(ResolvedOutputDirectory outputDirectory, ILogger<LogDatabaseFactory> logger) : IDisposable {
	private const string MonthFormat = "yyyy-MM";

	private static readonly Regex MonthFilePattern = new(@"^(?<env>.+)-(?<month>\d{4}-\d{2})\.db$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

	private readonly Dictionary<string, LogDatabase> _databases = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>The database holding <paramref name="date"/>'s blobs for <paramref name="environment"/>.</summary>
	public LogDatabase Get(string environment, DateOnly date) {
		string folder = EnvironmentFolder(environment);
		string path = Path.Combine(folder, $"{environment.ToLowerInvariant()}-{FormatMonth(date)}.db");

		if (!_databases.TryGetValue(path, out LogDatabase? database)) {
			logger.LogInformation("Opening {Database} for {Environment}.", path, environment);

			Directory.CreateDirectory(folder);
			database = new LogDatabase(path);
			_databases[path] = database;
		}

		return database;
	}

	/// <summary>The bookkeeping of all of an environment's monthly databases, merged into one index.</summary>
	public DatabaseIndex LoadIndex(string environment) {
		List<DateOnly> dates = [];
		List<DayCheck> checks = [];
		HashSet<(DateOnly Date, string CheckKey)> failedKeys = [];

		foreach ((_, DateOnly month) in MonthFiles(EnvironmentFolder(environment))) {
			DatabaseIndex index = Get(environment, month).LoadIndex();
			dates.AddRange(index.Dates);
			checks.AddRange(index.Checks);
			failedKeys.UnionWith(index.FailedKeys);
		}

		dates.Sort();
		return new DatabaseIndex(dates, checks, failedKeys);
	}

	/// <summary>
	/// Deletes, in every environment folder of the data directory, the monthly databases (and their
	/// WAL/shared-memory files) of months before <paramref name="cutoff"/>.
	/// </summary>
	public void DeleteExpired(DateOnly cutoff) {
		foreach (string folder in Directory.EnumerateDirectories(outputDirectory.Path)) {
			foreach ((string path, DateOnly month) in MonthFiles(folder).ToList()) {
				if (month >= cutoff) {
					continue;
				}

				if (_databases.Remove(path, out LogDatabase? open)) {
					open.Dispose();
				}

				foreach (string file in (string[])[path, path + "-wal", path + "-shm"]) {
					File.Delete(file);
				}

				logger.LogInformation("Deleted {Database}: older than the retention period.", path);
			}
		}
	}

	/// <summary>The first day of the month <paramref name="date"/> is in.</summary>
	public static DateOnly MonthOf(DateOnly date) => new(date.Year, date.Month, 1);

	public void Dispose() {
		foreach (LogDatabase database in _databases.Values) {
			database.Dispose();
		}

		_databases.Clear();
	}

	private string EnvironmentFolder(string environment) => Path.Combine(outputDirectory.Path, environment.ToLowerInvariant());

	/// <summary>
	/// The monthly databases in <paramref name="folder"/>: only "{folder name}-yyyy-MM.db" files
	/// count, so nothing else in the folder is ever opened or deleted.
	/// </summary>
	private static IEnumerable<(string Path, DateOnly Month)> MonthFiles(string folder) {
		if (!Directory.Exists(folder)) {
			yield break;
		}

		string environment = Path.GetFileName(folder);
		foreach (string path in Directory.EnumerateFiles(folder, "*.db")) {
			Match match = MonthFilePattern.Match(Path.GetFileName(path));
			if (match.Success &&
					string.Equals(match.Groups["env"].Value, environment, StringComparison.OrdinalIgnoreCase) &&
					DateOnly.TryParseExact(match.Groups["month"].Value, MonthFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly month)) {
				yield return (path, month);
			}
		}
	}

	private static string FormatMonth(DateOnly date) => date.ToString(MonthFormat, CultureInfo.InvariantCulture);
}
