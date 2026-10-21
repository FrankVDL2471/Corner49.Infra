using Corner49.Logs.Domain;

namespace Corner49.Logs.Parsing;

internal static class LogLevelNormalizer {
	/// <summary>Normalizes known level spellings to a canonical string; preserves anything unrecognized verbatim rather than discarding it.</summary>
	public static string Normalize(string? rawLevel) {
		LogEventLevel parsed = Parse(rawLevel);
		return parsed == LogEventLevel.Unknown && !string.IsNullOrWhiteSpace(rawLevel)
				? rawLevel
				: parsed.ToString();
	}

	public static LogEventLevel Parse(string? rawLevel) {
		return rawLevel?.Trim().ToLowerInvariant() switch {
			"trace" => LogEventLevel.Trace,
			"debug" => LogEventLevel.Debug,
			"information" or "informational" or "info" => LogEventLevel.Information,
			"warning" or "warn" => LogEventLevel.Warning,
			"error" => LogEventLevel.Error,
			"critical" or "fatal" => LogEventLevel.Critical,
			_ => LogEventLevel.Unknown
		};
	}
}
