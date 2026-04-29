using System.Text.Json;

namespace Corner49.Infra.Http {
	public class ApiClientOptions {

		public Action<JsonSerializerOptions> JsonOptions { get; set; }

		public int? RateLimit { get; set; }


	}
}
