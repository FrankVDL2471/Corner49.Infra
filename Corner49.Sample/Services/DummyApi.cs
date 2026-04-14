using Corner49.Infra.Http;
using System.Text.Json;

namespace Corner49.Sample.Services {
	public class DummyApi : ApiClient {
		public DummyApi() : base("https://dummyjson.com", false, opt => {
			opt.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
			opt.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;	
		}) {
		}


		protected override void OnRequest(string method, string url, string? request, string? response, bool? isSuccess) {
			base.OnRequest(method, url, request, response, isSuccess);
		}


		public Task<DummyResponse?> Test() {
			return base.Get<DummyResponse>("/test");
		}	

	}


	public class DummyResponse {

		public string? Status { get; set; }
		public string? Method { get; set;  }

	}
}
