using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Corner49.Infra.Http {
	public class OAuthClient {

		private readonly Uri _tokenUrl;
		private readonly string _clientId;
		private readonly string _clientSecret;
		private readonly string _scope;

		public OAuthClient(Uri tokenUrl, string clientId, string clientSecret, string scope) {
			_tokenUrl = tokenUrl;
			_clientId = clientId;
			_clientSecret = clientSecret;
			_scope = scope;
		}

		

		public async Task<OAuthResponse?> GetToken() {

			using (var httpClient = new HttpClient { BaseAddress = new Uri($"https://{_tokenUrl.Host}") }) {
				// Form data is typically sent as key-value pairs
				var formData = new List<KeyValuePair<string, string>>
				{
								new KeyValuePair<string, string>("client_id", _clientId),
								new KeyValuePair<string, string>("client_secret", _clientSecret),
								new KeyValuePair<string, string>("scope", _scope),
								new KeyValuePair<string, string>("grant_type", "client_credentials")
						};

				// Encodes the key-value pairs for the ContentType 'application/x-www-form-urlencoded'
				HttpContent content = new FormUrlEncodedContent(formData);

				try {
					// Send a POST request to the specified Uri as an asynchronous operation.
					HttpResponseMessage response = await httpClient.PostAsync(_tokenUrl.PathAndQuery, content);
					string result = await response.Content.ReadAsStringAsync();

					// Ensure we get a successful response.
					response.EnsureSuccessStatusCode();

					// Read the response as a string.
					return  JsonSerializer.Deserialize<OAuthResponse>(result);

				} catch (HttpRequestException e) {
					throw;
				}
			}
			return null;
		}

	}

	public class OAuthResponse {
		[JsonPropertyName("token_type")]
		public string? TokenType { get; set; }

		[JsonPropertyName("scope")]
		public string? Scope { get; set; }

		[JsonPropertyName("expires_in")]
		public int ExpiresIn { get; set; }

		[JsonPropertyName("ext_expires_in")]
		public int ExtExpiresIn { get; set; }

		[JsonPropertyName("access_token")]
		public string? AccessToken { get; set; }
	}

}
