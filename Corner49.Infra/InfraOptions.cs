using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Corner49.Infra {
	public class InfraOptions {

		public bool EnableAuthentication { get; set; } = false;

		public Action<IEndpointRouteBuilder> RouteBuilder { get; set; }

		public Action<Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder> Cors { get; set; }

		public Action<IApplicationBuilder> Middleware { get; set; }

	}
}
