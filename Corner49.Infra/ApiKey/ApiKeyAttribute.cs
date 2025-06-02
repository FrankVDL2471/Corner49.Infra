using Microsoft.AspNetCore.Mvc;

namespace Corner49.Infra.ApiKey {

	public class ApiKeyAttribute : ServiceFilterAttribute {
		public ApiKeyAttribute()
				: base(typeof(ApiKeyAuthFilter)) {
		}
	}
}
