using Corner49.Logs.Domain;
using System.Text.Json;

namespace Corner49.Logs.Parsing;

/// <summary>
/// Parses Azure App Service "AppServiceHTTPLogs" diagnostic export lines (see
/// Logs\api-http-logs.json). The payload ("properties") is a JSON string with
/// IIS/W3C-style field names (CsMethod, CsUriStem, ScStatus, TimeTaken, ...). Unlike console
/// records there is no "Level" field anywhere on an HTTP record - it is derived from
/// ScStatus/Result as a documented heuristic, not read from the data.
/// </summary>
public sealed class HttpLogParser : ILogParser {
	public bool CanParse(string line) {
		return AzureDiagnosticEnvelope.TryGetCategory(line, out string? category)
				&& category == "AppServiceHTTPLogs";
	}

	public LogEvent? Parse(string line, string sourceBlob, long lineNumber) {
		using JsonDocument document = JsonDocument.Parse(line);
		JsonElement root = document.RootElement;

		(string? service, string? environment) = AzureDiagnosticEnvelope.ParseResourceId(root);

		LogEvent logEvent = new() {
			Timestamp = AzureDiagnosticEnvelope.ParseTimestamp(root),
			EventType = LogEventType.Http.ToString(),
			SourceBlob = sourceBlob,
			SourceLineNumber = lineNumber,
			ImportedAt = DateTimeOffset.UtcNow
		};

		Dictionary<string, string> properties = [];

		string? propertiesJson = AzureDiagnosticEnvelope.GetString(root, "properties");
		if (propertiesJson is null) {
			logEvent.Properties = properties;
			return logEvent;
		}

		JsonDocument innerDocument;
		try {
			innerDocument = JsonDocument.Parse(propertiesJson);
		} catch (JsonException) {
			logEvent.Properties = properties;
			return logEvent;
		}

		using (innerDocument) {
			JsonElement inner = innerDocument.RootElement;
			if (inner.ValueKind != JsonValueKind.Object) {
				logEvent.Properties = properties;
				return logEvent;
			}

			string? method = AzureDiagnosticEnvelope.GetString(inner, "CsMethod");
			string? path = AzureDiagnosticEnvelope.GetString(inner, "CsUriStem");
			string? query = AzureDiagnosticEnvelope.GetString(inner, "CsUriQuery");
			string? statusText = AzureDiagnosticEnvelope.GetString(inner, "ScStatus");
			// TimeTaken is a bare JSON number in the real data (unlike ScStatus, which is a
			// quoted string) - GetString would silently return null for it.
			string? timeTakenText = inner.TryGetProperty("TimeTaken", out JsonElement timeTakenElement)
					? AzureDiagnosticEnvelope.ElementToString(timeTakenElement)
					: null;
			string? result = AzureDiagnosticEnvelope.GetString(inner, "Result");

			logEvent.HttpMethod = method;
			logEvent.HttpPath = string.IsNullOrEmpty(query) ? path : $"{path}?{query}";

			int? statusCode = int.TryParse(statusText, out int parsedStatus) ? parsedStatus : null;
			logEvent.StatusCode = statusCode;
			logEvent.DurationMs = long.TryParse(timeTakenText, out long parsedDuration) ? parsedDuration : null;
			logEvent.Level = DeriveLevel(statusCode);
			logEvent.Message = $"{method} {logEvent.HttpPath} -> {statusText}";

			AddIfPresent(properties, inner, "CsHost");
			AddIfPresent(properties, inner, "CIp");
			AddIfPresent(properties, inner, "UserAgent");

			if (result is not null) {
				properties["Result"] = result;
			}
		}

		logEvent.Properties = properties;
		return logEvent;
	}

	/// <summary>
	/// Heuristic, not ground truth: no real HTTP sample in Logs\*.json shows a
	/// 4xx-other or 5xx status, so this boundary is not confirmed against real data.
	/// </summary>
	private static string? DeriveLevel(int? statusCode) {
		return statusCode switch {
			null => null,
			< 400 => LogEventLevel.Information.ToString(),
			< 500 => LogEventLevel.Warning.ToString(),
			_ => LogEventLevel.Error.ToString()
		};
	}

	private static void AddIfPresent(Dictionary<string, string> properties, JsonElement root, string propertyName) {
		string? value = AzureDiagnosticEnvelope.GetString(root, propertyName);
		if (value is not null) {
			properties[propertyName] = value;
		}
	}
}
