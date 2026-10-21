namespace Corner49.Logs.Storage;

/// <summary>
/// Azure Diagnostic Settings always exports App Service / Function App logs into these 4
/// containers, each holding the same SUBSCRIPTIONS/.../SITES/.../y=/m=/d=/h= directory
/// structure. Every import run checks all of them unless restricted to one.
/// </summary>
public static class DiagnosticLogContainers {
	public const string ConsoleLogs = "insights-logs-appserviceconsolelogs";
	public const string HttpLogs = "insights-logs-appservicehttplogs";
	public const string FunctionAppLogs = "insights-logs-functionapplogs";
	public const string AppLogs = "insights-logs-appserviceapplogs";

	public static readonly IReadOnlyList<string> All = [ConsoleLogs, HttpLogs, FunctionAppLogs, AppLogs];
}
