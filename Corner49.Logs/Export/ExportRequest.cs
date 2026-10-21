namespace Corner49.Logs.Export;

/// <summary>
/// SubscriptionId/ResourceGroup/SiteName are all optional - null or empty means "all" at that
/// level (and, transitively, everything below it; see DiagnosticBlobPath).
/// Since/Until bound the UTC days (the blob's y=/m=/d= partition) to export. When Since is
/// given, every day from Since through Until (default: today) is listed in Azure, even days
/// the manifests already record as complete. When it's omitted, the export is incremental -
/// see <see cref="LogExporter"/>.
/// </summary>
public sealed record ExportRequest(
		string? SubscriptionId,
		string? ResourceGroup,
		string? SiteName,
		IReadOnlyList<string> Containers,
		DateOnly? Since,
		DateOnly? Until);
