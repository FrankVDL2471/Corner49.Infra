using Corner49.Logs.Export;
using Corner49.Logs.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.CommandLine;

namespace Corner49.Logs.CLI;

/// <summary>
/// Builds the root command. Exporting logs is the tool's only job, so the export options and
/// action live directly on the root command instead of behind a subcommand - running the CLI
/// with just the environment (and options) runs the export.
/// </summary>
public static class RootCommandFactory {
	public static RootCommand Build() {
		Argument<string> envArgument = new("env") {
			Description = "Environment name (e.g. dev, prod). The storage account connection string is read from the " +
						"'{env}-connectstring' setting in appsettings.json or an environment variable.",
		};

		Option<string?> dataDirOption = new("--data-dir") {
			Description = $"Directory for the SQLite databases. Defaults to the '{LogsHost.DataDirKey}' setting " +
						$"(appsettings.json or environment variable DATA_DIR), else '{LogsHost.DefaultDataDir}' next to the executable.",
		};

		Option<string?> subscriptionOption = new("--subscription") {
			Description = "Azure subscription ID the app's logs live under. Omit to export all subscriptions.",
		};

		Option<string?> resourceGroupOption = new("--resource-group") {
			Description = "Resource group the app belongs to. Omit to export all resource groups.",
		};

		Option<string?> siteOption = new("--site") {
			Description = "App Service/Function App site name whose logs to export. Omit to export all sites.",
		};

		Option<string?> containerOption = new("--container") {
			Description = "Restrict the export to a single insights-logs-* container " +
						$"(one of: {string.Join(", ", DiagnosticLogContainers.All)}). Omit to check all 4."
		};

		Option<DateOnly?> sinceOption = new("--since") {
			Description = "Only export log days (UTC) on or after this date. Every day from here through --until (default: today) is re-checked against Azure, even days already complete. Never earlier than Export:MaxAgeDays days ago. Omit for an incremental export."
		};

		Option<DateOnly?> untilOption = new("--until") {
			Description = "Only export log days (UTC) on or before this date."
		};

		RootCommand root = new("Corner49.Logs - exports Azure App Service / Functions diagnostic logs from a storage account into local SQLite databases.");
		root.Add(envArgument);
		root.Add(dataDirOption);
		root.Add(subscriptionOption);
		root.Add(resourceGroupOption);
		root.Add(siteOption);
		root.Add(containerOption);
		root.Add(sinceOption);
		root.Add(untilOption);

		root.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) => {
			string? container = parseResult.GetValue(containerOption);
			IReadOnlyList<string> containers;
			if (string.IsNullOrWhiteSpace(container)) {
				containers = DiagnosticLogContainers.All;
			} else if (DiagnosticLogContainers.All.Contains(container, StringComparer.OrdinalIgnoreCase)) {
				containers = [container];
			} else {
				Console.Error.WriteLine(
						$"Unknown container '{container}'. Must be one of: {string.Join(", ", DiagnosticLogContainers.All)}");
				return 1;
			}

			ExportRequest request = new(
					parseResult.GetValue(subscriptionOption),
					parseResult.GetValue(resourceGroupOption),
					parseResult.GetValue(siteOption),
					containers,
					parseResult.GetValue(sinceOption),
					parseResult.GetValue(untilOption));

			try {
				using IHost host = LogsHost.Build(parseResult.GetValue(envArgument)!, parseResult.GetValue(dataDirOption));
				using IServiceScope scope = host.Services.CreateScope();
				LogExporter exporter = scope.ServiceProvider.GetRequiredService<LogExporter>();

				ExportResult result = await exporter.ExportAsync(request, cancellationToken);

				Console.WriteLine();
				Console.WriteLine("Export completed.");
				Console.WriteLine($"  Files found:            {result.FilesFound}");
				Console.WriteLine($"  Files exported:         {result.FilesImported}");
				Console.WriteLine($"  Files skipped:          {result.FilesSkipped}");
				Console.WriteLine($"  Files failed:           {result.FilesFailed}");
				Console.WriteLine($"  Lines processed:        {result.LinesParsed}");
				Console.WriteLine($"  Unparsed lines:         {result.LinesUnparsed}");
				Console.WriteLine($"  Total events exported:  {result.EventsImported}");
				Console.WriteLine($"  Days already complete:  {result.DaysAlreadyComplete}");

				return result.FilesFailed > 0 ? 1 : 0;
			} catch (OperationCanceledException) {
				throw;
			} catch (Exception ex) {
				Console.Error.WriteLine($"Export failed: {ex.Message}");
				return 1;
			}
		});

		return root;
	}
}
