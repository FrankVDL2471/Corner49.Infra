using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Text;

namespace Corner49.Infra.Logging {
	public class AppInsightsEnrichMiddleware {

		private readonly RequestDelegate _next;

		public AppInsightsEnrichMiddleware(RequestDelegate next) {
			this._next = next;
		}


		public async Task InvokeAsync(HttpContext context) {
			var request = context.Request;

			var requestTelemetry = context.Features.Get<RequestTelemetry>();
			if (requestTelemetry == null) {
				await _next(context);
				return;
			}




			try {
				//Include body
				if (request?.Body?.CanRead == true) {
					request.EnableBuffering();
					var bodySize = (int)(request.ContentLength ?? request.Body.Length);
					if (bodySize > 0) {
						request.Body.Position = 0;

						string body;

						using (var ms = new MemoryStream(bodySize)) {
							await request.Body.CopyToAsync(ms);
							body = Encoding.UTF8.GetString(ms.ToArray());
						}

						request.Body.Position = 0;
						AddBody(requestTelemetry.Properties, "RequestBody", body);
					}
				}
			} catch (Exception err) {
				Console.Error.WriteLine($"Write RequestBody failed : {err.Message}");
			}

			await _next(context);

			try {
				if (context.Response?.Body?.CanRead == true) {
					var bodySize = (int)(context.Response.ContentLength ?? context.Response.Body.Length);
					if (bodySize > 0) {
						context.Response.Body.Position = 0;

						string body;

						using (var ms = new MemoryStream(bodySize)) {
							await context.Response.Body.CopyToAsync(ms);
							body = Encoding.UTF8.GetString(ms.ToArray());
						}

						context.Response.Body.Position = 0;
						AddBody(requestTelemetry.Properties, "ResponseBody", body);
					}
				}
			} catch (Exception err) {
				Console.Error.WriteLine($"Write ResponseBody failed : {err.Message}");
			}

		}


		public static void AddBody(IDictionary<string, string> properties, string key, string body) {
			if (body.Length < 8000) {
				if (!properties.ContainsKey(key)) {
					properties.Add(key, body);
				}
			} else {
				int idx = 0;
				while (true) {
					int len = body.Length - (idx * 8000);
					if (len <= 0) break;
					string part = body.Substring(idx * 8000, Math.Min(len, 8000));

					idx++;
					if (!properties.ContainsKey(key + idx.ToString("00"))) {
						properties.Add(key + idx.ToString("00"), part);
					}
				}


			}

		}
	}


	public static class AppInsightsEnrichmentExtension {

		public static IApplicationBuilder UseAppInsightsEnrichment(this IApplicationBuilder builder) {
			builder.UseMiddleware<AppInsightsEnrichMiddleware>();
			return builder;
		}
	}
}
