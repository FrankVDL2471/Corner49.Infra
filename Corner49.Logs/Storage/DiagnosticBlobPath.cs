using System.Globalization;

namespace Corner49.Logs.Storage;

/// <summary>
/// Builds the blob-name prefixes Azure Diagnostic Settings writes under every
/// insights-logs-* container:
/// resourceId=/SUBSCRIPTIONS/{sub}/RESOURCEGROUPS/{rg}/PROVIDERS/MICROSOFT.WEB/SITES/{site}/
/// followed by y=/m=/d=/h=/m=00/PT1H.json. The keyword segments and resource names are
/// upper-cased because that's how Azure itself writes them.
/// </summary>
public static class DiagnosticBlobPath {
	/// <summary>
	/// Builds the longest prefix Azure Blob Storage can filter on server-side from whichever
	/// of subscriptionId/resourceGroup/siteName are provided, stopping at the first one
	/// that's missing - a blob-name prefix can only be built from the front of the path. With
	/// the missing value left out, the prefix ends right where that value's segment would
	/// start, so listing the virtual directories below it yields the candidates for that level
	/// (e.g. BuildPrefix(sub, null, null) lists the subscription's resource groups).
	/// </summary>
	public static string BuildPrefix(string? subscriptionId, string? resourceGroup, string? siteName) {
		string prefix = "resourceId=/SUBSCRIPTIONS/";

		if (string.IsNullOrWhiteSpace(subscriptionId)) {
			return prefix;
		}

		prefix += subscriptionId.ToUpperInvariant() + "/RESOURCEGROUPS/";

		if (string.IsNullOrWhiteSpace(resourceGroup)) {
			return prefix;
		}

		prefix += resourceGroup.ToUpperInvariant() + "/PROVIDERS/MICROSOFT.WEB/SITES/";

		if (string.IsNullOrWhiteSpace(siteName)) {
			return prefix;
		}

		return prefix + siteName.ToUpperInvariant() + "/";
	}

	/// <summary>
	/// Narrows a full site prefix (from <see cref="BuildPrefix"/> with all three values) to the
	/// blobs of one UTC day.
	/// </summary>
	public static string BuildDatePrefix(string sitePrefix, DateOnly date) =>
			sitePrefix + date.ToString("'y='yyyy'/m='MM'/d='dd'/'", CultureInfo.InvariantCulture);
}
