using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Corner49.Infra.Auth {
	public class AuthSettings {

		public string? Domain { get; set; }
		public string? ClientId { get; set; }
		public string? ClientSecret { get; set; }
		public string? Audience { get; set; }

		/// <summary>
		/// Api Identifier for Auth0 Management API
		/// </summary>
		public string? ApiIdentifier { get; set; }
	}
}
