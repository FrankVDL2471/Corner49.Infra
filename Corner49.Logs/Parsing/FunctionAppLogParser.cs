using Corner49.Logs.Domain;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Corner49.Logs.Parsing;

/// <summary>
/// Parses Azure Functions "FunctionAppLogs" diagnostic export lines (see
/// Logs\functions-logs.json). Added because a real third format showed up in
/// the sample set beyond the two named in the original spec - the task explicitly invites
/// this ("design so additional formats can easily be added"). Two things make this format
/// different from the other two: the outer "time" is MM/dd/yyyy HH:mm:ss rather than
/// ISO-8601 (handled by AzureDiagnosticEnvelope.ParseTimestamp), and its "properties"
/// payload is single-quoted pseudo-JSON (Python-repr style), NOT valid JSON, so it cannot be
/// parsed with JsonDocument - this parser uses tolerant regex extraction instead, and keeps
/// the full raw properties string so nothing is lost even where extraction under-matches.
/// </summary>
public sealed class FunctionAppLogParser : ILogParser {
	private static readonly Regex QuotedFieldPattern = new(@"'(?<key>[A-Za-z0-9_]+)'\s*:\s*'(?<value>(?:[^'\\]|\\.)*)'", RegexOptions.Compiled);

	public bool CanParse(string line) {
		return AzureDiagnosticEnvelope.TryGetCategory(line, out string? category)
				&& category == "FunctionAppLogs";
	}

	public LogEvent? Parse(string line, string sourceBlob, long lineNumber) {
		using JsonDocument document = JsonDocument.Parse(line);
		JsonElement root = document.RootElement;


		LogEvent logEvent = new() {
			Timestamp = AzureDiagnosticEnvelope.ParseTimestamp(root),
			EventType = LogEventType.Function.ToString(),
			SourceBlob = sourceBlob,
			SourceLineNumber = lineNumber,
			ImportedAt = DateTimeOffset.UtcNow
		};

		Dictionary<string, string> properties = [];

		string? propertiesText = AzureDiagnosticEnvelope.GetString(root, "properties");
		if (propertiesText is not null) {
			properties["RawProperties"] = propertiesText;

			logEvent.Message = ExtractQuotedValue(propertiesText, "message");

			string? level = ExtractQuotedValue(propertiesText, "level");
			logEvent.Level = level is not null ? LogLevelNormalizer.Normalize(level) : null;

			// functionInvocationId ties together one function execution's
			// Started/Trigger/Completed lines - confirmed in the sample.
			logEvent.SpanId = ExtractQuotedValue(propertiesText, "functionInvocationId");

			AddIfPresent(properties, propertiesText, "functionName");
			AddIfPresent(properties, propertiesText, "eventName");
			AddIfPresent(properties, propertiesText, "category");
			AddIfPresent(properties, propertiesText, "appName");
			AddIfPresent(properties, propertiesText, "hostInstanceId");
		}

		logEvent.Properties = properties;
		return logEvent;
	}

	private static void AddIfPresent(Dictionary<string, string> properties, string propertiesText, string key) {
		string? value = ExtractQuotedValue(propertiesText, key);
		if (value is not null) {
			properties[key] = value;
		}
	}

	private static string? ExtractQuotedValue(string source, string key) {
		foreach (Match match in QuotedFieldPattern.Matches(source)) {
			if (match.Groups["key"].Value == key) {
				return match.Groups["value"].Value.Replace("\\'", "'");
			}
		}

		return null;
	}
}
