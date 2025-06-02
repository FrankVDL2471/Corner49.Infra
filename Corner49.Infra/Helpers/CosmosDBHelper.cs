namespace Corner49.Infra.Helpers {
	public static class CosmosDBHelper {
		public static string? GetAuthSecret(string connectString) {
			return GetConnectstringPart(connectString, "AccountKey");
		}
		public static string? GetUrl(string connectString) {
			return GetConnectstringPart(connectString, "AccountEndpoint");
		}


		public static string? GetConnectstringPart(string connectString, string key) {
			if (string.IsNullOrEmpty(connectString)) return null;
			string[] flds = connectString.Split(';');
			foreach (string fld in flds) {
				int idx = fld.IndexOf('=');
				string nm = fld.Substring(0, idx);
				string val = fld.Substring(idx + 1);

				if (nm.Equals(key, StringComparison.OrdinalIgnoreCase)) return val;
			}

			return null;
		}
	}
}
