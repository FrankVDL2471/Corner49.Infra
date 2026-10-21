using Corner49.Logs.Domain;
using System.Globalization;
using System.Text.Json;

namespace Corner49.Logs.Parsing;

/// <summary>
/// Parses Azure App Service "AppServiceConsoleLogs" diagnostic export lines (see the samples
/// Logs\api-console-logs.json, app-console-logs.json, jobs-console-logs.json).
/// The payload ("resultDescription") is a JSON string that is *usually* structured ILogger
/// output (the .NET JSON console formatter), but is sometimes plain text - confirmed in the real samples: dotnet startup
/// ASCII-art banners and ad hoc Console.WriteLine debug output. Both are legitimate console
/// output, not parse failures, and both produce a non-Unparsed LogEvent.
/// </summary>
public sealed class ConsoleLogParser : ILogParser {
	public bool CanParse(string line) {
		return AzureDiagnosticEnvelope.TryGetCategory(line, out string? category)
				&& category == "AppServiceConsoleLogs";
	}

	public LogEvent? Parse(string line, string sourceBlob, long lineNumber) {
		using JsonDocument document = JsonDocument.Parse(line);
		JsonElement root = document.RootElement;


		Dictionary<string, string> properties = [];

		LogEvent logEvent = new() {
			Timestamp = AzureDiagnosticEnvelope.ParseTimestamp(root),
			EventType = LogEventType.Console.ToString(),
			SourceBlob = sourceBlob,
			SourceLineNumber = lineNumber,
			ImportedAt = DateTimeOffset.UtcNow
		};

		string? resultDescription = AzureDiagnosticEnvelope.GetString(root, "resultDescription");

		if (resultDescription is null || !TryParseStructuredPayload(resultDescription, properties, logEvent)) {
			// Plain-text case: real console output that isn't a structured ILogger line
			// (startup banners, ad hoc Console.WriteLine debug output). Not an unparsed line.
			logEvent.Message = resultDescription;
			string? outerLevel = AzureDiagnosticEnvelope.GetString(root, "level");
			logEvent.Level = outerLevel is not null ? LogLevelNormalizer.Normalize(outerLevel) : null;
		}

		logEvent.Properties = properties;
		return logEvent;
	}

	private static bool TryParseStructuredPayload(string resultDescription, Dictionary<string, string> properties, LogEvent logEvent) {
		JsonDocument innerDocument;
		try {
			innerDocument = JsonDocument.Parse(resultDescription);
		} catch (JsonException) {
			return false;
		}

		using (innerDocument) {
			JsonElement inner = innerDocument.RootElement;
			if (inner.ValueKind != JsonValueKind.Object) {
				return false;
			}

			string? level = AzureDiagnosticEnvelope.GetString(inner, "LogLevel");
			logEvent.Level = level is not null ? LogLevelNormalizer.Normalize(level) : null;
			logEvent.Message = AzureDiagnosticEnvelope.GetString(inner, "Message");
			logEvent.Category = AzureDiagnosticEnvelope.GetString(inner, "Category");

			string? timestamp = AzureDiagnosticEnvelope.GetString(inner, "Timestamp");
			if (timestamp is not null && TryParseTimestamp(timestamp, out DateTimeOffset parsed)) {
				logEvent.Timestamp = parsed;
			}

			logEvent.Exception = AzureDiagnosticEnvelope.GetString(inner, "Exception");
			if (inner.TryGetProperty("EventId", out JsonElement eventId) && eventId.ValueKind == JsonValueKind.Number && eventId.GetInt32() != 0) {
				properties["EventId"] = eventId.GetRawText();
			}

			MergeState(inner, properties);
			MergeScopes(inner, properties, logEvent);
			MapRequestFields(properties, logEvent);

			return true;
		}
	}

	/// <summary>
	/// The app's JsonConsoleFormatterOptions.TimestampFormat decides this format. Tried in order:
	/// "dd-MM-yyyy HH:mm:ss.fffzzz" (e.g. "28-08-2026 19:01:15.123+02:00"), "dd-MM-yyyy HH:mm:ss"
	/// without offset (local time of the app, read as local time of the machine running the export),
	/// then anything DateTimeOffset parses invariantly (ISO 8601 and the like).
	/// </summary>
	private static bool TryParseTimestamp(string timestamp, out DateTimeOffset parsed) {
		if (DateTimeOffset.TryParseExact(timestamp, "dd-MM-yyyy HH:mm:ss.fffzzz", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)) {
			return true;
		}
		if (DateTime.TryParseExact(timestamp, "dd-MM-yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime legacy)) {
			parsed = legacy;
			return true;
		}
		return DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out parsed);
	}

	/// <summary>
	/// Request logging middleware typically logs method/status/duration as state: put them in the
	/// same columns as the platform HTTP logs, whatever the category. Values already set from
	/// the scopes win.
	/// </summary>
	private static void MapRequestFields(Dictionary<string, string> properties, LogEvent logEvent) {
		if (logEvent.HttpMethod is null && properties.TryGetValue("Method", out string? method)) {
			logEvent.HttpMethod = method;
		}
		if (logEvent.StatusCode is null && properties.TryGetValue("StatusCode", out string? status) && int.TryParse(status, CultureInfo.InvariantCulture, out int statusCode)) {
			logEvent.StatusCode = statusCode;
		}
		if (logEvent.DurationMs is null
				&& (properties.TryGetValue("ElapsedMs", out string? elapsed) || properties.TryGetValue("DurationMs", out elapsed))
				&& double.TryParse(elapsed, CultureInfo.InvariantCulture, out double elapsedMs)) {
			logEvent.DurationMs = (long)Math.Round(elapsedMs);
		}
		if (logEvent.Route is null && properties.TryGetValue("Route", out string? route)) {
			logEvent.Route = route;
		}
	}

	private static void MergeState(JsonElement inner, Dictionary<string, string> properties) {
		if (!inner.TryGetProperty("State", out JsonElement stateElement) || stateElement.ValueKind != JsonValueKind.Object) {
			return;
		}

		foreach (JsonProperty stateProperty in stateElement.EnumerateObject()) {
			if (stateProperty.Name == "{OriginalFormat}") {
				continue;
			}

			string? value = AzureDiagnosticEnvelope.ElementToString(stateProperty.Value);
			if (value is not null) {
				properties[stateProperty.Name] = value;
			}
		}
	}

	private static void MergeScopes(JsonElement inner, Dictionary<string, string> properties, LogEvent logEvent) {
		if (!inner.TryGetProperty("Scopes", out JsonElement scopesElement) || scopesElement.ValueKind != JsonValueKind.Array) {
			return;
		}

		foreach (JsonElement scope in scopesElement.EnumerateArray()) {
			if (scope.ValueKind != JsonValueKind.Object) {
				continue;
			}

			foreach (JsonProperty scopeProperty in scope.EnumerateObject()) {
				if (scopeProperty.Name == "Message") {
					continue;
				}

				string? value = AzureDiagnosticEnvelope.ElementToString(scopeProperty.Value);
				if (value is null) {
					continue;
				}

				// Case-insensitive: apps write both PascalCase and camelCase scope keys (TraceId, traceId, ...).
				// Anything not mapped to a column lands in properties.
				switch (scopeProperty.Name.ToLowerInvariant()) {
					case "traceid":
						logEvent.TraceId = value;
						break;
					case "spanid":
						logEvent.SpanId = value;
						break;
					case "requestid":
						logEvent.RequestId = value;
						break;
					case "requestpath":
						logEvent.HttpPath = value;
						break;
					case "actionname":
						logEvent.ActionName = value;
						break;
					case "route":
						logEvent.Route = value;
						break;
					case "userid":
						logEvent.UserId = value;
						break;
					case "parentid":
					case "connectionid":
					case "actionid":
					case "{originalformat}":
						// Noise: not useful for investigations.
						break;
					default:
						properties[scopeProperty.Name] = value;
						break;
				}
			}
		}
	}
}
