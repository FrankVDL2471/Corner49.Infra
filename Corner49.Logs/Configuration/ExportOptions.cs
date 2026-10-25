namespace Corner49.Logs.Configuration;

/// <summary>Bound from the "Export" configuration section.</summary>
public sealed record ExportOptions {
	/// <summary>
	/// Log days (UTC) older than this many days before today are never exported, whatever
	/// --since says or what's already on disk.
	/// </summary>
	public int MaxAgeDays { get; init; } = 90;

	/// <summary>
	/// Full months kept besides the current one: at the end of every export, the monthly
	/// databases of older months are deleted (3 in October keeps July through October).
	/// Log days in those older months are never exported either.
	/// </summary>
	public int RetentionMonths { get; init; } = 3;
}
