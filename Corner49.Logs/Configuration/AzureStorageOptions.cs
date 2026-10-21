namespace Corner49.Logs.Configuration;

/// <summary>
/// The storage account holding the insights-logs-* containers. <see cref="ConnectionString"/>
/// is read from the "{env}-connectstring" setting for the environment passed on the command
/// line (see CLI.LogsHost).
/// </summary>
public sealed record AzureStorageOptions {
	public string? ConnectionString { get; init; }
}
