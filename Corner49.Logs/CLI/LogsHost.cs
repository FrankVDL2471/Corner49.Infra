using Corner49.Logs.Configuration;
using Corner49.Logs.Export;
using Corner49.Logs.Export.Database;
using Corner49.Logs.Infrastructure;
using Corner49.Logs.Parsing;
using Corner49.Logs.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Corner49.Logs.CLI;

/// <summary>
/// Builds the host for one export run, once the command line is parsed. Configuration comes from
/// appsettings.json (+ appsettings.{DOTNET_ENVIRONMENT}.json) next to the executable and from
/// environment variables - never from the command line, which System.CommandLine owns.
/// </summary>
public static class LogsHost {
	/// <summary>Setting holding the data directory when --data-dir is omitted (environment variable DATA_DIR).</summary>
	public const string DataDirKey = "data_dir";

	/// <summary>Data directory used when neither --data-dir nor <see cref="DataDirKey"/> is set; relative to the executable.</summary>
	public const string DefaultDataDir = "data";

	/// <summary>Setting holding the storage connection string for an environment, e.g. "prod-connectstring".</summary>
	public static string ConnectionStringKey(string env) => $"{env}-connectstring";

	/// <exception cref="InvalidOperationException">No connection string is configured for <paramref name="env"/>.</exception>
	public static IHost Build(string env, string? dataDir) {
		// Content root = the executable's folder, so appsettings.json is found whatever the
		// current directory is when the standalone exe is started.
		HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings {
			ContentRootPath = AppContext.BaseDirectory,
			Args = [],
		});

		string? connectionString = builder.Configuration[ConnectionStringKey(env)];
		if (string.IsNullOrWhiteSpace(connectionString)) {
			throw new InvalidOperationException(
					$"No connection string configured for environment '{env}'. " +
					$"Set '{ConnectionStringKey(env)}' in appsettings.json or as an environment variable.");
		}

		builder.Services.AddSingleton(Options.Create(new AzureStorageOptions { ConnectionString = connectionString }));
		builder.Services.AddOptions<ExportOptions>()
				.Bind(builder.Configuration.GetSection("Export"))
				.Validate(options => options.MaxAgeDays > 0, "Export:MaxAgeDays must be a positive number of days.")
				.Validate(options => options.RetentionMonths > 0, "Export:RetentionMonths must be a positive number of months.");

		string configuredDataDir = !string.IsNullOrWhiteSpace(dataDir)
				? dataDir
				: builder.Configuration[DataDirKey] is { Length: > 0 } fromConfig ? fromConfig : DefaultDataDir;
		builder.Services.AddSingleton(new ResolvedOutputDirectory(OutputDirectoryInitializer.EnsureOutputDirectoryExists(configuredDataDir)));

		builder.Services.AddSingleton<ILogStorageReader, AzureBlobLogStorageReader>();
		builder.Services.AddSingleton(TimeProvider.System);

		// Registration order matters for LogParserDispatcher: most specific first. Dispatch is by
		// the "category" field in the real data, not guesswork, so order mostly just needs to put
		// the fallback last.
		builder.Services.AddSingleton<ILogParser, HttpLogParser>();
		builder.Services.AddSingleton<ILogParser, ConsoleLogParser>();
		builder.Services.AddSingleton<ILogParser, FunctionAppLogParser>();
		builder.Services.AddSingleton<GenericFallbackLogParser>();
		builder.Services.AddSingleton<LogParserDispatcher>();

		builder.Services.AddSingleton<LogDatabaseFactory>();
		builder.Services.AddScoped<LogExporter>();

		return builder.Build();
	}
}
