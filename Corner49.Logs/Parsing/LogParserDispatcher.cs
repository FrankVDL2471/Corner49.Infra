using Corner49.Logs.Domain;

namespace Corner49.Logs.Parsing;

/// <summary>
/// Tries each registered <see cref="ILogParser"/> in order; the first whose CanParse matches
/// wins. Falls back to <see cref="GenericFallbackLogParser"/> (never null, never throws) so
/// the importer can always continue.
/// </summary>
public sealed class LogParserDispatcher(IEnumerable<ILogParser> parsers, GenericFallbackLogParser fallbackParser) {
	public LogEvent Parse(string line, string sourceBlob, long lineNumber) {
		foreach (ILogParser parser in parsers) {
			if (!parser.CanParse(line)) {
				continue;
			}

			LogEvent? parsed = parser.Parse(line, sourceBlob, lineNumber);
			if (parsed is not null) {
				return parsed;
			}
		}

		return fallbackParser.Parse(line, sourceBlob, lineNumber);
	}
}
