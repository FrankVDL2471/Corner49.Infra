using System.Globalization;
using System.Text.RegularExpressions;

namespace Corner49.Logs.Export;

/// <summary>
/// The environment/service/date/hour a blob's output should be partitioned under, parsed
/// straight from the blob name itself (see the path shape documented on
/// <see cref="Storage.DiagnosticBlobPath"/>) rather than from parsed line content - this keeps
/// every blob's output path deterministic even when every line inside it fails to parse.
/// </summary>
public sealed record BlobPathSegments(string Environment, string Service, DateOnly Date, int Hour) {
	private static readonly Regex PathPattern = new(
			@"RESOURCEGROUPS/(?<rg>[^/]+)/PROVIDERS/MICROSOFT\.WEB/SITES/(?<site>[^/]+)/y=(?<y>\d{4})/m=(?<m>\d{2})/d=(?<d>\d{2})/h=(?<h>\d{2})",
			RegexOptions.Compiled | RegexOptions.IgnoreCase);

	public static bool TryParse(string blobName, out BlobPathSegments? segments) {
		Match match = PathPattern.Match(blobName);
		if (!match.Success) {
			segments = null;
			return false;
		}

		string environment = match.Groups["rg"].Value;
		string service = match.Groups["site"].Value;
		int year = int.Parse(match.Groups["y"].Value, CultureInfo.InvariantCulture);
		int month = int.Parse(match.Groups["m"].Value, CultureInfo.InvariantCulture);
		int day = int.Parse(match.Groups["d"].Value, CultureInfo.InvariantCulture);
		int hour = int.Parse(match.Groups["h"].Value, CultureInfo.InvariantCulture);

		segments = new BlobPathSegments(environment, service, new DateOnly(year, month, day), hour);
		return true;
	}
}
