using Corner49.CLI.Jobs;
using Corner49.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.Diagnostics;
using System.Text.Json;

namespace Corner49.CLI {
	public class InfraBuilderInstance {
		public string? Name { get; set; }
		public IServiceProvider? Services { get; set; }
	}

	public class InfraBuilder : IInfraBuilder {

		private readonly IHostApplicationBuilder _builder;
		private readonly IServiceCollection _services;
		private readonly string _appName;

		public InfraBuilder(IHostApplicationBuilder builder, string appName) {
			_builder = builder;
			_services = _builder.Services;
			_appName = appName;

			Instance = new InfraBuilderInstance { Name = appName };
		}


		public static InfraBuilder Create(string name, params string[] args) {
			var builder = Host.CreateApplicationBuilder(args);

			return builder.UseInfra(name);
		}



		public static InfraBuilderInstance Instance { get; private set; } = new InfraBuilderInstance();

		public IConfigurationManager Configuration => _builder.Configuration;

		public IServiceCollection Services { get => _services; }

		public string Name { get => _appName; }


		#region Configuration


		public InfraBuilder WithOptions<T>(string? configSection = null) where T : class {
			if (configSection == null) {
				configSection = typeof(T).Name;
				if (configSection.EndsWith("Configuration")) configSection = configSection.Substring(0, configSection.Length - "Configuration".Length);
				if (configSection.EndsWith("Options")) configSection = configSection.Substring(0, configSection.Length - "Options".Length);
			}

			_services.Configure<T>(Configuration.GetSection(configSection));

			return this;
		}

		#endregion

		#region Logging


		private LoggingOptions? _loggingOptions = null;


		public InfraBuilder WithLogging(Action<LoggingOptions>? options = null) {
			_loggingOptions = new LoggingOptions();
			if (options != null) options(_loggingOptions);

			if (_loggingOptions.TrackActivity) {
				_builder.Logging.Configure(options => {
					options.ActivityTrackingOptions =
							ActivityTrackingOptions.TraceId |
							ActivityTrackingOptions.SpanId |
							ActivityTrackingOptions.ParentId;
				});
			}

			if (_loggingOptions.WriteToConsoleAsJson) {
				_builder.Logging.AddJsonConsole(log => {
					log.IncludeScopes = _loggingOptions.TrackActivity;
					log.UseUtcTimestamp = false;
					log.TimestampFormat = "dd-MM-yyyy HH:mm:ss";
					log.JsonWriterOptions = new JsonWriterOptions {
						Indented = false,
					};
				});
			} else if (IsLocalEnvironment || Environment.GetEnvironmentVariable("ConsoleLog") == "true") {
				_builder.Logging.AddConsole();
			}


			return this;
		}

		public static bool IsLocalEnvironment => Debugger.IsAttached || Environment.GetEnvironmentVariable("IsLocalEnv") == "True";

		public static bool IsDevelopment(string environment) {
			return "Development".Equals(environment, StringComparison.OrdinalIgnoreCase);
		}


		#endregion


		#region CommandLine

		private RootCommand? _rootCommand = null;

		public InfraBuilder WithCommandLine(Action<RootCommand> cmd, string? description) {
			_rootCommand = new RootCommand(description ?? string.Empty);
			cmd(_rootCommand);
			return this;
		}



		#endregion


		#region Jobs 

		private Dictionary<string, string>? _jobs = null;

		public InfraBuilder AddJob<T>(string jobName, string? description = null) where T : class, IJob {
			if (_jobs == null) _jobs = new Dictionary<string, string>();
			if (_jobs.ContainsKey(jobName)) throw new InvalidOperationException($"Job '{jobName}' already registered.");

			_jobs.Add(jobName, description ?? string.Empty);

			_services.AddKeyedSingleton<IJob, T>(jobName.ToLowerInvariant());


			return this;
		}


		#endregion



		#region Extensions


		private List<InfraExtension> _extensions = new List<InfraExtension>();

		public IInfraBuilder AddExtension(InfraExtension ext)  {
			_extensions.Add(ext);
			return this;	
		}


		#endregion


		public async Task BuildAndRun(params string[] args) {
			if (_builder is HostApplicationBuilder host) {
				foreach(var ext in _extensions) {
					await ext.Build(this, this.Configuration);
				}	

				var app = host.Build();
				InfraBuilder.Instance.Services = app.Services;


				foreach (var ext in _extensions) {
					await ext.Start(app.Services);
				}


				if (_rootCommand != null) {
					var cmd = _rootCommand.Parse(args);

					if (cmd.Errors?.Any() == true) {
						Console.WriteLine("Command Linke Error: " + string.Join(", ", cmd.Errors.Select(c => c.Message)));
					} else {
						await cmd.InvokeAsync();
					}
				} else if (_jobs?.Any() == true) {
					if (args?.Any() != true) {
						Console.WriteLine("No Job specified. Available jobs:");
						foreach (var job in _jobs) {
							Console.WriteLine($"- {job.Key}: {job.Value}");
						}


					} else {
						string job = args.First().ToLowerInvariant();
						var jobService = app.Services.GetKeyedService<IJob>(job);
						if (jobService == null) {
							Console.Error.WriteLine($"Job '{job}' not found.");
						} else {
							await jobService.Execute(args);
						}
					}
				} else {
					await app.RunAsync();
				}
			}

		}







	}
}
