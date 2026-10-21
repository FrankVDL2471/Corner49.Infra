namespace Corner49.Logs.Infrastructure;

/// <summary>
/// Single reusable place enforcing "never log secrets" for anything connection-string shaped.
/// </summary>
public static class ConnectionStringRedactor {
	public static string RedactAzureStorageConnectionString(string? connectionString) {
		if (string.IsNullOrWhiteSpace(connectionString)) {
			return "(not set)";
		}

		foreach (string part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries)) {
			int separatorIndex = part.IndexOf('=');
			if (separatorIndex <= 0) {
				continue;
			}

			string key = part[..separatorIndex].Trim();
			if (key.Equals("AccountName", StringComparison.OrdinalIgnoreCase)) {
				return part[(separatorIndex + 1)..].Trim();
			}
		}

		return "(redacted)";
	}
}
