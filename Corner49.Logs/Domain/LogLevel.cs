namespace Corner49.Logs.Domain;

/// <summary>
/// Parser-side helper for normalizing raw level strings from different log sources.
/// <see cref="LogEvent.Level"/> itself stays a plain string so unrecognized/future
/// level names are never lost.
/// </summary>
public enum LogEventLevel {
	Trace,
	Debug,
	Information,
	Warning,
	Error,
	Critical,
	Unknown
}
