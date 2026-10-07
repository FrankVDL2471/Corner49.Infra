namespace Corner49.Infra.Tools {
	public static class Base64 {

		public static string? Encode(string? plainText) {
			if (string.IsNullOrEmpty(plainText)) return null;

			var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
			return System.Convert.ToBase64String(plainTextBytes);
		}

		public static string? Decode(string? base64EncodedData) {
			if (string.IsNullOrEmpty(base64EncodedData)) return null;

			var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
			return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
		}

	}
}
