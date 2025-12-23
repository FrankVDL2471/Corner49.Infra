using Auth0.AspNetCore.Authentication;
using Corner49.Infra.DB;
using Corner49.Infra.Health;
using Corner49.Infra.Jobs;
using Corner49.Infra.Logging;
using Corner49.Infra.ServiceBus;
using Corner49.Infra.Tools;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scalar.AspNetCore;
using System.Diagnostics;
using System.Text.Json.Serialization;
using Corner49.Infra.Helpers;
using Corner49.Infra.ApiKey;
using System.Text.Json;
using Microsoft.AspNetCore.OpenApi;
using Corner49.Infra.Auth;

namespace Corner49.Infra {
	public class InfraBuilder {

		private readonly IHostApplicationBuilder _builder;
		private readonly IServiceCollection _services;
		private readonly string _appName;

		public InfraBuilder(WebApplicationBuilder builder, string appName) {
			_builder = builder;
			_services = _builder.Services;
			_appName = appName;

			//Internal services
			_services.AddSingleton<IServiceBusService, ServiceBusService>();
			_services.AddSingleton<ITelemetryService, TelemetryService>();


			Configuration = builder.Configuration;
			Instance = this;
		}
		public InfraBuilder(HostApplicationBuilder builder, string appName) {
			_builder = builder;
			_services = _builder.Services;
			_appName = appName;

			//Internal services
			_services.AddSingleton<IServiceBusService, ServiceBusService>();
			_services.AddSingleton<ITelemetryService, TelemetryService>();


			Configuration = builder.Configuration;
			Instance = this;
		}



		public static InfraBuilder Instance { get; private set; }

		public ConfigurationManager Configuration;

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


			var appinsights = Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"] ?? Configuration["AppInsights:ConnectionString"];
			if (!string.IsNullOrEmpty(appinsights)) {

				_services.AddApplicationInsightsTelemetry((opt) => {
					opt.ConnectionString = appinsights;
					opt.EnableDependencyTrackingTelemetryModule = _loggingOptions.TrackDependencies;
#if DEBUG
					opt.DeveloperMode = true;
#endif
				});

				if (_loggingOptions.AzureWebAppDiagnostics) {
					_builder.Logging.AddAzureWebAppDiagnostics(cfg => {
						cfg.BlobName = this.Name + ".log";
						cfg.IncludeScopes = true;
						cfg.IsEnabled = true;
					});
				}



				if (_loggingOptions.WriteToConsoleAsJson) {
					_builder.Logging.AddJsonConsole(log => {
						log.IncludeScopes = false;
						log.UseUtcTimestamp = false;
						log.TimestampFormat = "dd-MM-yyyy HH:mm:ss";
						log.JsonWriterOptions = new JsonWriterOptions {
							Indented = false,
						};
					});
				} else if (IsLocalEnvironment || Environment.GetEnvironmentVariable("ConsoleLog") == "true") {
					_builder.Logging.AddConsole();
				}
				if (_loggingOptions.FilterCategoryPrefix != null) {
					_builder.Logging.AddFilter((cat, level) => {
						if ((level == Microsoft.Extensions.Logging.LogLevel.Error) || (level == Microsoft.Extensions.Logging.LogLevel.Critical)) return true;
						foreach (var prefix in _loggingOptions.FilterCategoryPrefix) {
							if (cat?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true) return true;
						}
						return false;
					});
				}


				AppInsightsTelemetryProcessor.LongRequestThreshold = _loggingOptions.TrackLongRequestThreshold;
				if (_loggingOptions.IngoreRequestsPaths != null) {
					foreach (var path in _loggingOptions.IngoreRequestsPaths) {
						AppInsightsTelemetryProcessor.AddPath(path);
					}
				}

				_services.AddSingleton<ITelemetryInitializer, AppInsightsTelemetryInitializer>();
				_services.AddApplicationInsightsTelemetryProcessor<AppInsightsTelemetryProcessor>();
			}

			//_builder.Logging.add


			//if (_builder is WebApplicationBuilder webBuilder) {
			//	webBuilder.Host.UseSerilog((hostingContext, services, loggerConfiguration) => {
			//		// Avoid passing instrumentation key directly to Serilog sink. This creates a new TelemetryConfiguration internally resulting to logs being NOT correlated
			//		// Use TelemetryConfiguration from DI instead.
			//		loggerConfiguration
			//				.ReadFrom.Configuration(hostingContext.Configuration)
			//				.ApplyDefaults(
			//						this.Name,
			//						hostingContext.HostingEnvironment.EnvironmentName,
			//						string.IsNullOrEmpty(appinsights) ? null : services.GetService<TelemetryConfiguration>(),
			//						writeToConsole: _loggingOptions.WriteToConsole || InfraBuilder.IsDevelopment(hostingContext.HostingEnvironment.EnvironmentName) || IsLocalEnvironment || Environment.GetEnvironmentVariable("ConsoleLog") == "true");
			//	});
			//}



			return this;
		}

		public static bool IsLocalEnvironment => Debugger.IsAttached || Environment.GetEnvironmentVariable("IsLocalEnv") == "True";

		public static bool IsDevelopment(string environment) {
			return "Development".Equals(environment, StringComparison.OrdinalIgnoreCase);
		}


		private string? _heathCheck = null;
		private IHealthChecksBuilder? _healtChecks;

		public InfraBuilder WithHealthCheck(string path = "/health") {
			_heathCheck = path;
			_healtChecks = this.Services.AddHealthChecks();
			return this;
		}


		#endregion


		#region Authentication

		private string? _auth = null;

		private AuthSettings? _authSettings = null;

		public InfraBuilder WithAuth0(Action<AuthSettings>? options = null) {
			_auth = "auth0";

			_authSettings = new AuthSettings();
			_authSettings.Domain = this.Configuration["Auth0:Domain"];
			_authSettings.Audience = this.Configuration["Auth0:Audience"];
			_authSettings.ClientId = this.Configuration["Auth0:ClientId"];
			_authSettings.ClientSecret = this.Configuration["Auth0:ClientSecret"];

			if (options != null) options(_authSettings);

			this.Services.Configure<AuthSettings>((cfg) => {
				cfg.Domain = _authSettings.Domain;
				cfg.Audience = _authSettings.Audience;
				cfg.ClientId = _authSettings.ClientId;
				cfg.ClientSecret = _authSettings.ClientSecret;
			});

			if (string.IsNullOrEmpty(_authSettings.Domain)) throw new ArgumentNullException("Domain", "AuthSettings.Domain is not set");


			if (_hasApiControllers) {
				if (string.IsNullOrEmpty(_authSettings.Audience)) throw new ArgumentNullException("Audience", "AuthSettings.Audience is not set");

				//Auth0
				_services.AddAuthentication(options => {
					options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
					options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;

				}).AddJwtBearer(options => {
					options.Authority = $"https://{_authSettings.Domain}/";
					options.Audience = _authSettings.Audience;
				});
				_services.AddAuthorization();
			}

			if (_hasViewControllers) {
				if (string.IsNullOrEmpty(_authSettings.ClientId)) throw new ArgumentNullException("ClientId", "AuthSettings.ClientId is not set");

				this.Services.AddAuth0WebAppAuthentication(options => {
					options.Domain = _authSettings?.Domain;
					options.ClientId = _authSettings?.ClientId;
					options.ClientSecret = _authSettings?.ClientSecret;
				});
				this.Services.ConfigureSameSiteNoneCookies();
			}


			return this;
		}

		public InfraBuilder WithApiKey<T>() where T : class, IApiKeyValidation {
			//_auth = "ApiKey";

			Services.AddTransient<IApiKeyValidation, T>();
			Services.AddScoped<ApiKeyAuthFilter>();

			return this;
		}


		#endregion

		#region Controllers

		private bool _hasApiControllers = false;

		public InfraBuilder WithApiControllers(string apiVersion = "v1", Action<System.Text.Json.JsonSerializerOptions>? jsonOptions = null, Action<OpenApiOptions> openApi = null) {
			_hasApiControllers = true;

			//_services.AddResponseCompression(options => {
			//	options.EnableForHttps = true;
			//});

			//_services.AddHealthChecks()
			if (_healtChecks != null) {
				_healtChecks.AddCheck<HealthService>(_appName);
			}


			_services.AddOpenApi((options) => {
				if (_auth == "auth0") {
					options.AddDocumentTransformer<Auth0SchemeTransformer>();
				}

				options.AddDocumentTransformer((doc, context, cancelToken) => {
					doc.Info.Title = _appName;
					doc.Info.Version = apiVersion;
					return Task.CompletedTask;
				});
				if (openApi != null) {
					openApi.Invoke(options);	
				} else {
					options.BuildSchemas();
					options.BuildOperations();
				}
			});

			_services.AddControllers()
			.AddJsonOptions(options => {
				options.JsonSerializerOptions.SetDefault();
				if (jsonOptions != null) {
					jsonOptions.Invoke(options.JsonSerializerOptions);
				}
			});

			return this;

		}

		private bool _hasViewControllers;
		public InfraBuilder WithViewControllers(Action<System.Text.Json.JsonSerializerOptions>? jsonOptions = null, Func<IMvcBuilder, IMvcBuilder>? mvcBuilder = null) {
			_hasViewControllers = true;

			IMvcBuilder mvc = _services.AddControllersWithViews()
				.AddRazorRuntimeCompilation();
			if (mvcBuilder != null) mvc = mvcBuilder(mvc);

			mvc.AddJsonOptions(options => {
				options.JsonSerializerOptions.SetDefault();
				if (jsonOptions != null) {
					jsonOptions.Invoke(options.JsonSerializerOptions);
				}
			});

			return this;
		}



		private string? _corsPolicy = null;
		public InfraBuilder WithCors(params string[] origins) {
			_corsPolicy = $"cors_{_appName}";

			_services.AddCors(options => {
				options.AddPolicy(_corsPolicy, (p) => {
					p.AllowAnyHeader();
					p.AllowAnyMethod();

					if (origins?.Any() == true) {
						p.AllowCredentials();
						p.WithOrigins(origins);
					} else {
						p.AllowAnyOrigin();
					}
				});
			});


			return this;
		}

		public InfraBuilder WithCors(Func<string, bool> isAllowedOrigin) {
			_corsPolicy = $"cors_{_appName}";

			_services.AddCors(options => {
				options.AddPolicy(_corsPolicy, (p) => {
					p.AllowAnyHeader();
					p.AllowAnyMethod();
					p.AllowAnyOrigin();
					p.SetIsOriginAllowed(isAllowedOrigin);
				});
			});


			return this;
		}



		#endregion

		#region ServiceBus


		public InfraBuilder AddServiceBus(Action<ServiceBusConfiguration>? config = null) {
			this.Services.Configure<ServiceBusConfiguration>((cfg) => {
				this.Configuration.GetSection(ServiceBusConfiguration.SectionName).Bind(cfg);
				if (config != null) {
					config(cfg);
				}
			});

			return this;

		}


		/// <summary>
		/// Link a Processor to a ServiceBus queue or topic
		/// </summary>
		/// <typeparam name="T">PubSubProcessor implementation</typeparam>
		/// <param name="options">ServiceBus confiruation</param>
		/// <returns></returns>
		public InfraBuilder AddServiceBusHandler<T>(Action<IServiceBusOptions>? options = null) where T : class, IServiceBusHandler {
			_services.AddHostedService((srv) => {
				var logger = srv.GetRequiredService<ILogger<T>>();
				var config = srv.GetRequiredService<IConfiguration>();
				var bus = srv.GetRequiredService<IServiceBusService>();
				var tc = srv.GetService<TelemetryClient>();

				ServiceBusOptions opt = new ServiceBusOptions(typeof(T).Name);
				if (options != null) options.Invoke(opt);

				if (string.IsNullOrEmpty(opt.Name)) {
					throw new ArgumentNullException("Name", "ServiceBusOptions.Name must be set");
				}

				return new ServiceBusTrigger<T>(logger, tc, srv, bus, opt);
			});
			return this;
		}

		#endregion

		#region SignalR

		public InfraBuilder AddSignalRHub(string connectionString) {
			_services.AddSignalR().AddAzureSignalR(connectionString)
							.AddJsonProtocol(c => {
								c.PayloadSerializerOptions = c.PayloadSerializerOptions.SetDefault();
								c.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
							});
			return this;
		}

		public InfraBuilder AddSignalRClient(string hubClassName, string connectionString) {
			var serviceManager = new ServiceManagerBuilder()
											.WithOptions(option => {
												option.ConnectionString = connectionString;
												option.ServiceTransportType = ServiceTransportType.Persistent;
											})
											.BuildServiceManager();


			_services.AddSingleton(serviceManager);

			var serviceHubContext = serviceManager.CreateHubContextAsync(hubClassName, CancellationToken.None).Result;
			_services.AddSingleton<IServiceHubContext>(serviceHubContext);

			return this;
		}

		#endregion

		#region Jobs

		private JobBuilder _jobs = null;

		public InfraBuilder AddJobs(Action<JobBuilder> builder, Action<JobConfig> config = null) {
			JobConfig cfg = new JobConfig();
			cfg.ConnectString = this.Configuration["CosmosDB:ConnectString"];
			cfg.DbName = this.Configuration["CosmosDB:DBName"];
			cfg.ContainerName = "jobs";
			cfg.EnableDashboard = true;
			if (config != null) config(cfg);

			_jobs = new JobBuilder(Services, cfg);
			if (builder != null) {
				builder(_jobs);
			}
			return this;
		}




		#endregion

		#region DocumentDB

		private DocumentDBBuilder _docDBBuilder = null;

		public InfraBuilder AddDocumentDB(Action<DocumentDBBuilder>? repos = null) {
			_docDBBuilder = this.Services.AddDocumentDB(this.Configuration, repos);
			return this;
		}

		#endregion


		#region Sessions

		private bool _sessions;

		public InfraBuilder AddSession(Action<SessionOptions>? options = null) {
			_sessions = true;
			if (options != null) {
				this.Services.AddSession(options);
			} else {
				this.Services.AddSession(options => {
					options.IdleTimeout = TimeSpan.FromSeconds(30);
					options.Cookie.HttpOnly = false;
					options.Cookie.IsEssential = true;
				});
			}

			return this;
		}


		#endregion

		private bool _showDeveloperError = false;
		private string? _errorPage = null;
		public InfraBuilder WithErrorHandler(bool showDeveloperError, string? errorPage = null) {
			_showDeveloperError = showDeveloperError;
			_errorPage = errorPage;
			return this;
		}


		public async Task BuildAndRun(Func<WebApplication, Task>? afterBuild = null, Action<WebApplication>? map = null, string? fallbackUrl = null) {
			var app = (_builder as WebApplicationBuilder).Build();



			if (afterBuild != null) {
				await afterBuild(app);
			}

			if (_loggingOptions?.TrackContent == true) {
				app.UseMiddleware<AppInsightsEnrichMiddleware>();
			}

			// Configure the HTTP request pipeline.
			if (app.Environment.IsDevelopment()) {
			} else {
				if (_hasViewControllers) {
					if (_showDeveloperError) {
						app.UseDeveloperExceptionPage();
					} else { 
						app.UseExceptionHandler(_errorPage ?? "/Home/Error");
					}					
				}
				// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
				app.UseHsts();
			}

			app.UseHttpsRedirection();
			if (_hasViewControllers) {
				app.UseStaticFiles();
				if (_auth == "auth0") {
					app.UseCookiePolicy();
				}
			}

			if (_hasApiControllers) {
				app.MapOpenApi();
				app.MapScalarApiReference((options) => {
					options.Title = _appName;
					options.WithDownloadButton(true);
					if (_auth == "auth0") {
						options.WithOAuth2Authentication(new OAuth2Options {
							ClientId = _authSettings?.ClientId,
							Scopes = new[] { "openid", "OpenId" }
						});
					}
				});


			}
			if (_hasViewControllers) {
				app.UseRouting();
			}
			if (_corsPolicy != null) app.UseCors(_corsPolicy);

			if (_jobs != null) _jobs.UseDashboard(app, _appName);

			if (_auth != null) {
				app.UseAuthentication();
				app.UseAuthorization();
			}

			if (_sessions) {
				app.UseSession();
			}
			if (!string.IsNullOrWhiteSpace(_heathCheck)) {
				app.MapHealthChecks(_heathCheck);
			}

			if (_hasApiControllers) {
				app.MapControllers();
			}

			if (_hasViewControllers) {
				app.MapControllerRoute(
					name: "default",
					pattern: "{controller=Home}/{action=Index}/{id?}"
				);
				if (_auth == "auth0") {
					app.MapControllerRoute(
						name: "Identity",
						pattern: "Identity/{controller=Home}/{action=Index}",
						defaults: new { area = "Identity" }
					);
				}
				app.MapFallback(context => {
					context.Response.Redirect(fallbackUrl ?? "/Pages/ErrorPage");
					return Task.CompletedTask;
				});
			}
			if (map != null) {
				map(app);
			}




			if (_docDBBuilder != null) await _docDBBuilder.Init(app.Services);
			await app.RunAsync();
		}


		public Task BuildAndRun() {
			if (_builder is HostApplicationBuilder host) {
				var app = host.Build();
				return app.RunAsync();
			} else if (_builder is WebApplicationBuilder web) {
				return BuildAndRun(null, null, null);
			}
			return Task.CompletedTask;
		}







	}
}
