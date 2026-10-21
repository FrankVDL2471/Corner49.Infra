using System.Globalization;
using System.Text.Json;

namespace Corner49.Logs.Parsing;

/// <summary>
/// Parses the outer envelope shared by all three real log formats found in the samples
/// Logs\*.json (Azure App Service / Azure Functions diagnostic log export):
/// "time", "resourceId", "category", plus a "resultDescription" or "properties" payload
/// string. Used by all three concrete <see cref="ILogParser"/> implementations so this
/// logic exists in exactly one place.
/// </summary>
internal static class AzureDiagnosticEnvelope {
	public static bool TryGetCategory(string line, out string? category) {
		category = null;
		try {
			using JsonDocument document = JsonDocument.Parse(line);
			category = GetString(document.RootElement, "category");
			return category is not null;
		} catch (JsonException) {
			return false;
		}
	}

	/// <summary>
	/// "time" is ISO-8601 UTC (App Service records, e.g. 2026-08-01T02:00:08.34...Z) but
	/// MM/dd/yyyy HH:mm:ss (Azure Functions records, e.g. 08/28/2026 17:00:00) - confirmed
	/// from the real samples, not assumed. DateTimeOffset.TryParse with invariant culture
	/// handles both without needing per-format branching.
	/// </summary>
	public static DateTimeOffset ParseTimestamp(JsonElement root) {
		string? raw = GetString(root, "time");
		if (raw is not null &&
				DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset parsed)) {
			return parsed;
		}

		return DateTimeOffset.UtcNow;
	}

	/// <summary>
	/// resourceId is a full ARM resource path, e.g.
	/// /SUBSCRIPTIONS/.../RESOURCEGROUPS/MY-RG/PROVIDERS/MICROSOFT.WEB/SITES/MY-RG-API
	/// Service comes from the last path segment (the site name); Environment from the
	/// resource-group segment.
	/// </summary>
	public static (string? Service, string? Environment) ParseResourceId(JsonElement root) {
		string? resourceId = GetString(root, "resourceId");
		if (string.IsNullOrEmpty(resourceId)) {
			return (null, null);
		}

		string[] segments = resourceId.Split('/', StringSplitOptions.RemoveEmptyEntries);
		string? service = segments.Length > 0 ? segments[^1] : null;

		string? environment = null;
		int resourceGroupIndex = Array.FindIndex(
				segments, s => s.Equals("RESOURCEGROUPS", StringComparison.OrdinalIgnoreCase));
		if (resourceGroupIndex >= 0 && resourceGroupIndex + 1 < segments.Length) {
			environment = segments[resourceGroupIndex + 1];
		}

		return (service, environment);
	}

	public static string? GetString(JsonElement root, string propertyName) {
		return root.TryGetProperty(propertyName, out JsonElement element) && element.ValueKind == JsonValueKind.String
				? element.GetString()
				: null;
	}

	public static string? ElementToString(JsonElement element) => element.ValueKind switch {
		JsonValueKind.String => element.GetString(),
		JsonValueKind.Number => element.GetRawText(),
		JsonValueKind.True => "true",
		JsonValueKind.False => "false",
		JsonValueKind.Null => null,
		_ => element.GetRawText()
	};
}
