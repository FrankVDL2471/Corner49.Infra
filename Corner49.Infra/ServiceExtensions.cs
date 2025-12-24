using Corner49.Infra.Logging;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Corner49.Infra {
	public static class ServiceExtensions {



		public static IApplicationBuilder AddSignalRHub<T>(this IApplicationBuilder app, string hubName) where T : Hub {
			app.UseEndpoints(endpoints => {
				endpoints.MapHub<T>(hubName);
			});

			return app;
		}


		//public static IApplicationBuilder UseInfra(this IApplicationBuilder app, IWebHostEnvironment env, InfraOptions options = null) {

		//	app.UseResponseCompression();

		//	if (env.IsDevelopment()) {
		//		app.UseDeveloperExceptionPage();
		//	}

		//	if (InfraBuilder.Instance?.Name != null) {
				
		//		app.UseSwaggerUI(c => {
		//			c.DefaultModelsExpandDepth(-1); // Disable swagger schemas at bottom
		//			c.SwaggerEndpoint("/swagger/v1/swagger.json", InfraBuilder.Instance.Name);
		//			c.RoutePrefix = string.Empty;
		//		});

		//	}
		//	if (options?.Cors != null) {
		//		app.UseCors(options?.Cors);
		//	} else {
		//		// Solution for SignalR CORS issue. The API should allow any origin, SignalR should allow Credentials
		//		// `The value of the 'Access-Control-Allow-Origin' header in the response must not be the wildcard '*' when the request's credentials mode is 'include'.`
		//		// If this proves a problem in the future, research multiple CORS policies.
		//		app.UseCors(x => x
		//						.SetIsOriginAllowed(host => true)
		//						.AllowCredentials()
		//						.AllowAnyMethod()
		//						.AllowAnyHeader()
		//						.WithExposedHeaders("ErrorCode", "ErrorDescription"));
		//	}

		//	app.UseHttpsRedirection();
		//	app.UseRobotsTxt(env);
		//	app.UseRouting();

		//	app.UseAppInsightsEnrichment();
		//	if (options?.Middleware != null) {
		//		options.Middleware.Invoke(app);
		//	}

		//	if (options?.EnableAuthentication != false) {
		//		app.UseAuthentication();
		//		app.UseAuthorization();
		//	}


		//	app.UseEndpoints(endpoints => {
		//		endpoints.MapControllers();
		//		endpoints.MapHealthChecks("/health", new HealthCheckOptions {
		//			Predicate = _ => true,
		//			ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
		//		});


		//		if (options?.RouteBuilder != null) {
		//			options?.RouteBuilder.Invoke(endpoints);
		//		}

		//	});


		//	return app;

		//}


		public static InfraBuilder UseInfra(this WebApplicationBuilder builder, string appName, string environment = null) {
			builder.Configuration.AddInfra(environment ?? builder.Environment.EnvironmentName);
			//Dot not log request comming from the loggin system itself  (ex /health checks)
			builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", (level) => false);

			return new InfraBuilder(builder, appName);
		}
		public static InfraBuilder UseInfra(this HostApplicationBuilder builder, string appName, string environment = null) {
			builder.Configuration.AddInfra(environment ?? builder.Environment.EnvironmentName);
			//Dot not log request comming from the loggin system itself  (ex /health checks)
			builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", (level) => false);

			return new InfraBuilder(builder, appName);
		}
		public static InfraBuilder UseInfra(this IHostApplicationBuilder builder, ConfigurationManager config, string appName, string environment = null) {
			builder.Configuration.AddInfra(environment ?? builder.Environment.EnvironmentName);
			//Dot not log request comming from the loggin system itself  (ex /health checks)
			builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", (level) => false);

			return new InfraBuilder(builder, config, appName);
		}



		internal static IConfigurationBuilder AddInfra(this IConfigurationBuilder builder, string environmentName, string settingsFile = "appsettings") {
			var localConfig = new ConfigurationBuilder()
											.SetBasePath(Directory.GetCurrentDirectory())
											.AddJsonFile($"{settingsFile}.json", false, true)
											.AddJsonFile($"{settingsFile}.{environmentName}.json", true, true)
											.AddJsonFile($"{settingsFile}.{Environment.MachineName}.json", true, true)
											.AddEnvironmentVariables()
											.Build();


			var appConfig = localConfig["AppConfig"];
			if (!string.IsNullOrEmpty(appConfig)) {
				builder.AddAzureAppConfiguration((ctx) => {

					ctx.Connect(appConfig)
													.Select(KeyFilter.Any, LabelFilter.Null)
													.Select(KeyFilter.Any, environmentName) // Override with any configuration values specific to current hosting env
													.Select(KeyFilter.Any, Environment.MachineName); // Override with any configuration values specific to current hosting env
				});

			}
			//Changes in local appsettings files override the settings comming from appconfig
			builder.AddConfiguration(localConfig);

			return builder;

		}





	}
}
