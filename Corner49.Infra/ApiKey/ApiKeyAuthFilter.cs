using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Corner49.Infra.ApiKey {
	public class ApiKeyAuthFilter : IAuthorizationFilter {
		private readonly IApiKeyValidation _apiKeyValidation;

		public static readonly string ApiKeyHeaderName = "X-API-Key";


		public ApiKeyAuthFilter(IApiKeyValidation apiKeyValidation) {
			_apiKeyValidation = apiKeyValidation;
		}

		public void OnAuthorization(AuthorizationFilterContext context) {
			string userApiKey = context.HttpContext.Request.Headers[ApiKeyHeaderName].ToString();

			if (string.IsNullOrEmpty(userApiKey)) {
				userApiKey = context.HttpContext.Request?.Query["apiKey"];
			}


			if (string.IsNullOrEmpty(userApiKey)) {
				context.Result = new BadRequestResult();
				return;
			}

			if (!_apiKeyValidation.IsValidApiKey(userApiKey))
				context.Result = new UnauthorizedResult();
		}
	}
}
