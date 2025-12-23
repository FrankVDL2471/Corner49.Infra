using Corner49.Infra.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;

namespace Corner49.Infra.Auth {
	public interface IAuth0Service {
		Task<Auth0UserModel?> GetCurrentUser(HttpContext context);

		Task<Auth0UserModel?> GetUser(string email);
		Task<Auth0UserModel?> DeleteUser(string email);
	}

	public class Auth0Service : IAuth0Service {



		private readonly HttpClient _httpClient;
		private readonly AuthSettings _settings;

		public Auth0Service(IOptions<AuthSettings> options) {
			_settings = options.Value;

			_httpClient = new HttpClient();
			_httpClient.BaseAddress = new Uri($"https://{_settings.Domain}");
		}


		public async Task<Auth0UserModel?> GetCurrentUser(HttpContext context) {
			if (!context.Request.Headers.ContainsKey("Authorization")) return null;

			var header = context.Request.Headers.FirstOrDefault(c => c.Key == "Authorization");

			_httpClient.DefaultRequestHeaders.Clear();
			_httpClient.DefaultRequestHeaders.Add("Authorization", new string[] { header.Value });

			var data = await _httpClient.GetStringAsync("/userinfo");
			return JsonSerializer.Deserialize<Auth0UserModel>(data, JsonHelper.Options);


		}

		public async Task<Auth0UserModel?> GetUser(string email) {
			var token = await GetToken();
			if (token == null) return null;
			_httpClient.DefaultRequestHeaders.Clear();
			_httpClient.DefaultRequestHeaders.Add("authorization", $"Bearer {token}");



			var data = await _httpClient.GetStringAsync($"/api/v2/users-by-email?email={HttpUtility.UrlEncode(email)}");
			var usrs = JsonSerializer.Deserialize<Auth0UserModel[]>(data, JsonHelper.Options);
			return usrs?.FirstOrDefault();
		}

		public async Task<Auth0UserModel?> DeleteUser(string email) {
			var token = await GetToken();
			if (token == null) return null;

			_httpClient.DefaultRequestHeaders.Clear();
			_httpClient.DefaultRequestHeaders.Add("authorization", $"Bearer {token}");

			var rtrn = await _httpClient.GetAsync($"/api/v2/users-by-email?email={HttpUtility.UrlEncode(email)}");
			var body = await rtrn.Content.ReadAsStringAsync();

			var usr = JsonSerializer.Deserialize<Auth0UserModel[]>(body, JsonHelper.Options).FirstOrDefault();

			var resp = await _httpClient.DeleteAsync($"/api/v2/users/{usr.Id}");

			return usr;
		}


		public async Task<string?> GetToken() {
			var dict = new Dictionary<string, string>();
			dict.Add("grant_type", "client_credentials");
			dict.Add("client_id", _settings.ClientId);
			dict.Add("client_secret", _settings.ClientSecret);
			dict.Add("audience", _settings.ApiIdentifier);

			using var req = new HttpRequestMessage(HttpMethod.Post, "/oauth/token") { Content = new FormUrlEncodedContent(dict) };
			using var res = await _httpClient.SendAsync(req);
			if (!res.IsSuccessStatusCode) return null;

			var body = await res.Content.ReadAsStringAsync();

			var token = JsonSerializer.Deserialize<Auth0Token>(body, JsonHelper.Options);
			return token?.AccessToken;
		}




	}
}
