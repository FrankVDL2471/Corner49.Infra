using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Corner49.Infra.Logging {
	public class RobotsTxtMiddleware {
		const string Default =
						@"User-Agent: *\nDisallow: /";

		private readonly RequestDelegate next;
		private readonly string environmentName;
		private readonly string rootPath;

		public RobotsTxtMiddleware(
						RequestDelegate next,
						string environmentName,
						string rootPath
		) {
			this.next = next;
			this.environmentName = environmentName;
			this.rootPath = rootPath;
		}

		public async Task InvokeAsync(HttpContext context) {
			if (context.Request.Path.Value.StartsWith("/robots")) {
				var generalRobotsTxt = Path.Combine(rootPath, "robots.txt");
				var environmentRobotsTxt = Path.Combine(rootPath, $"robots.{environmentName}.txt");
				string output;

				// try environment first
				if (File.Exists(environmentRobotsTxt)) {
					output = await File.ReadAllTextAsync(environmentRobotsTxt);
				}
				// then robots.txt
				else if (File.Exists(generalRobotsTxt)) {
					output = await File.ReadAllTextAsync(generalRobotsTxt);
				}
				// then just a general default
				else {
					output = Default;
				}

				context.Response.ContentType = "text/plain";
				await context.Response.WriteAsync(output);
			} else {
				await next(context);
			}
		}
	}

	public static class RobotsTxtMiddlewareExtensions {
		public static IApplicationBuilder UseRobotsTxt(
						this IApplicationBuilder builder,
						IWebHostEnvironment env,
						string rootPath = null
		) {
			return builder.MapWhen(ctx => ctx.Request.Path.Value.StartsWith("/robots", StringComparison.OrdinalIgnoreCase), b =>
							b.UseMiddleware<RobotsTxtMiddleware>(env.EnvironmentName, rootPath ?? env.ContentRootPath));
		}
	}

}
