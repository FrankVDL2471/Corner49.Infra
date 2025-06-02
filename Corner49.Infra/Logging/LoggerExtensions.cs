using Microsoft.ApplicationInsights.Extensibility;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using Serilog;

namespace Corner49.Infra.Logging {

	internal static class LoggerExtensions {

		public const string OutputTemplate = "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}";

		/// <summary>
		///     Applies Formitable suggested configuration based on the environment the program is running in.
		/// </summary>
		/// <remarks>
		///     The following defaults are applied to the returned <see cref="LoggerConfiguration"/>:
		///     <list type="bullet">
		///         <item>Write logs to Console, Trace, Seq when <paramref name="environment"/> is Development.</item>
		///         <item>Write logs to Seq when <paramref name="writeToSeq"/> is true and <paramref name="environment"/> is Test.</item>
		///         <item>Write logs to Application Insights if <paramref name="telemetryConfiguration"/> is not null.</item>
		///     </list>
		/// </remarks>
		/// <param name="loggerConfiguration">
		///     The <see cref="LoggerConfiguration"/> to extend to.
		/// </param>
		/// <param name="component">
		///     The name of the component. (for example: FT.App.Api)
		/// </param>
		/// <param name="environment">
		///     The hosting environment name.
		/// </param>
		/// <param name="telemetryConfiguration">
		///     The Active Telemetry Configuration.
		/// </param>
		/// <param name="writeToConsole">
		///     (Optional) If true logger will write to console always.
		/// </param>
		/// <returns>
		///     The same instance of <see cref="LoggerConfiguration"/> for chaining.
		/// </returns>
		public static LoggerConfiguration ApplyDefaults(this LoggerConfiguration loggerConfiguration,
				string component,
				string environment,
				TelemetryConfiguration? telemetryConfiguration,
				bool writeToConsole = false) {

			loggerConfiguration
					.Enrich.FromLogContext()
					.Enrich.WithProperty("Component", component)
					.Enrich.WithProperty("Environment", environment)
					.MinimumLevel.Debug()
					.MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
					.MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
					.MinimumLevel.Override("System", LogEventLevel.Warning)
					.MinimumLevel.Override("Microsoft.AspNetCore.Authentication", LogEventLevel.Warning)
					.MinimumLevel.Override("Hangfire.Server", LogEventLevel.Warning);
					//.WriteTo.File($"logs/{component}-applog.txt",
					//		rollingInterval: RollingInterval.Day,
					//		outputTemplate: OutputTemplate,
					//		restrictedToMinimumLevel: LogEventLevel.Warning,
					//		retainedFileCountLimit: 3); ;


			if (InfraBuilder.IsDevelopment(environment) || writeToConsole) {
				loggerConfiguration
						.WriteTo.Console(
								restrictedToMinimumLevel: InfraBuilder.IsDevelopment(environment) ? LogEventLevel.Debug : LogEventLevel.Information,
								outputTemplate:
							 OutputTemplate,
								theme: AnsiConsoleTheme.Code);
			}

			if (telemetryConfiguration != null) {

				var telemetryConverter = TelemetryConverter.Events;
				loggerConfiguration.WriteTo.ApplicationInsights(telemetryConfiguration, telemetryConverter, LogEventLevel.Warning);
			}

			return loggerConfiguration;
		}
	}
}
