using Corner49.Logs.Domain;

namespace Corner49.Logs.Parsing;

/// <summary>
/// Catches lines whose outer envelope isn't valid JSON at all, or whose "category" matches
/// none of the known formats. Never throws, always returns a LogEvent so an import never
/// crashes on a malformed line - the raw line is preserved via RawMessage. Deliberately not
/// registered as an ILogParser in DI; <see cref="LogParserDispatcher"/> calls it directly as
/// the explicit last resort.
/// </summary>
public sealed class GenericFallbackLogParser {
	public LogEvent Parse(string line, string sourceBlob, long lineNumber) {
		return new LogEvent {
			Timestamp = DateTimeOffset.UtcNow,
			EventType = LogEventType.Unparsed.ToString(),
			Message = line,
			SourceBlob = sourceBlob,
			SourceLineNumber = lineNumber,
			ImportedAt = DateTimeOffset.UtcNow
		};
	}
}
