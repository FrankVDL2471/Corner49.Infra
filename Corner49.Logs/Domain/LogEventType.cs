namespace Corner49.Logs.Domain;

/// <summary>
/// Parser-side helper identifying which real-world log format produced a
/// <see cref="LogEvent"/>. <see cref="LogEvent.EventType"/> itself stays a plain string
/// (the enum's name) so the column stays queryable/forward-compatible without a migration
/// every time a new format is added.
/// </summary>
public enum LogEventType {
	Console,
	Http,
	Function,
	Unparsed
}
