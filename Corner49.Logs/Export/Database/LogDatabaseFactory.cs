using Corner49.Logs.Export;
using Microsoft.Extensions.Logging;

namespace Corner49.Logs.Export.Database;

/// <summary>
/// Opens (creating on first use) one <see cref="LogDatabase"/> per environment, stored as
/// {data dir}/{resource group lowercased}.db - e.g. my-app-dev.db and my-app-prd.db for the
/// MY-APP-DEV and MY-APP-PRD resource groups - and keeps it open for the rest of the run.
/// </summary>
public sealed class LogDatabaseFactory(ResolvedOutputDirectory outputDirectory, ILogger<LogDatabaseFactory> logger) : IDisposable {
	private readonly Dictionary<string, LogDatabase> _databases = new(StringComparer.OrdinalIgnoreCase);

	public LogDatabase Get(string environment) {
		if (!_databases.TryGetValue(environment, out LogDatabase? database)) {
			string path = Path.Combine(outputDirectory.Path, $"{environment.ToLowerInvariant()}.db");
			logger.LogInformation("Opening {Database} for {Environment}.", path, environment);

			database = new LogDatabase(path);
			_databases[environment] = database;
		}

		return database;
	}

	public void Dispose() {
		foreach (LogDatabase database in _databases.Values) {
			database.Dispose();
		}

		_databases.Clear();
	}
}
