using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Corner49.Infra.Auth {

	public class Auth0UserModel {


		[JsonPropertyName("user_id")]
		public string? Id { get; set; }


		[JsonPropertyName("email")]
		public string? Email { get; set; }

		[JsonPropertyName("email_verified")]
		public bool? EmailVerified { get; set; }

		[JsonPropertyName("created_at")]
		public string? CreatedAt { get; set; }


		[JsonPropertyName("given_name")]
		public string? FirstName { get; set; }

		[JsonPropertyName("family_name")]
		public string? LastName { get; set; }

		[JsonPropertyName("name")]
		public string? Name { get; set; }

		[JsonPropertyName("phone_number")]
		public string? Phone { get; set; }

		[JsonPropertyName("picture")]
		public string? Picture { get; set; }


		[JsonPropertyName("app_metadata")]
		public Dictionary<string, object>? AppMetaData { get; set; }

		[JsonPropertyName("user_metadata")]
		public Dictionary<string, object>? UserMetaData { get; set; }


		[JsonPropertyName("last_login")]
		public DateTimeOffset? LastLogin { get; set; }

		[JsonPropertyName("logins_count")]
		public int LoginsCount { get; set; }


		public string? GetAppMetaData(string key) {
			if (this.AppMetaData == null) return null;
			if (!this.AppMetaData.ContainsKey(key)) return null;

			var value = this.AppMetaData[key];
			if (value is string txt) return txt;
			if (value is JsonElement el) return el.GetString();

			return null;
		}

		public string? GetUserMetaData(string key) {
			if (this.UserMetaData == null) return null;
			if (!this.UserMetaData.ContainsKey(key)) return null;

			var value = this.UserMetaData[key];
			if (value is string txt) return txt;
			if (value is JsonElement el) return el.GetString();

			return null;
		}

	}
}
