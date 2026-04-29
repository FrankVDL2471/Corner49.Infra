using System.Text.Json.Serialization;

namespace Corner49.Infra.Auth {
	public class Auth0Token {

		[JsonPropertyName("access_token")]
		public string AccessToken { get; set; }

		[JsonPropertyName("expires_in")]
		public int ExpiresIn { get; set; }

		[JsonPropertyName("scope")]
		public string Scope { get; set; }

		[JsonPropertyName("token_type")]
		public string TokenType { get; set; }
	}
}
