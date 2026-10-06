using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace Corner49.CLI {
	public static class ServiceExtensions {

		public static InfraBuilder UseInfra(this IHostApplicationBuilder builder,string appName, string? environment = null) {
			builder.Configuration.AddInfra(environment ?? builder.Environment.EnvironmentName);
			//Dot not log request comming from the loggin system itself  (ex /health checks)
			builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", (level) => false);

			return new InfraBuilder(builder, appName);
		}



		internal static IConfigurationBuilder AddInfra(this IConfigurationBuilder builder, string environmentName, string settingsFile = "appsettings") {
			var localConfig = new ConfigurationBuilder()
											.SetBasePath(Directory.GetCurrentDirectory())
											.AddJsonFile($"{settingsFile}.json", true, true)
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
