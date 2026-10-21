namespace Corner49.Logs.Configuration;

/// <summary>Bound from the "Export" configuration section.</summary>
public sealed record ExportOptions {
	/// <summary>
	/// Log days (UTC) older than this many days before today are never exported, whatever
	/// --since says or what's already on disk.
	/// </summary>
	public int MaxAgeDays { get; init; } = 90;
}
