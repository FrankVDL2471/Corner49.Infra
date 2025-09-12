using Corner49.Infra.Helpers;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Corner49.Infra.Jobs {
	public class JobBuilder {

		private readonly IServiceCollection _services;
		private readonly Hangfire.Azure.CosmosDbStorage? _storage;
		private readonly JobConfig _config;

		public JobBuilder(IServiceCollection services,  JobConfig config) {
			_services = services;
			_config = config;	


			if (config.UseSqlServer) {
				SqlConnectionStringBuilder bld = new SqlConnectionStringBuilder(config.ConnectString);
				if (config.DbName != null) bld.InitialCatalog = config.DbName;

				GlobalConfiguration.Configuration
					.UseSqlServerStorage(bld.ToString(), new SqlServerStorageOptions {
						TryAutoDetectSchemaDependentOptions = false, // Defaults to `true`
						PrepareSchemaIfNecessary = true
					})
					.UseIgnoredAssemblyVersionTypeResolver()
					.UseSimpleAssemblyNameTypeSerializer();

				services.AddHangfire(x => x.UseSqlServerStorage(bld.ToString())  );
			} else {
				string? url = CosmosDBHelper.GetUrl(config.ConnectString);
				string? authSecret = CosmosDBHelper.GetAuthSecret(config.ConnectString);

				if (string.IsNullOrEmpty(url)) {
					Console.Error.WriteLine("JobBuilder failed : CosmosDB Url not set");
					return;
				}
				if (string.IsNullOrEmpty(authSecret)) {
					Console.Error.WriteLine("JobBuilder failed : CosmosDB AuthSecret not set");
					return;
				}

				_storage = Hangfire.Azure.CosmosDbStorage.Create(url, authSecret, config.DbName, config.ContainerName);
				GlobalConfiguration.Configuration
					.UseStorage(_storage)
					.UseIgnoredAssemblyVersionTypeResolver()
					.UseSimpleAssemblyNameTypeSerializer();

				services.AddHangfire(x => x.UseAzureCosmosDbStorage(url, authSecret, config.DbName, config.ContainerName));
			}

			services.AddHangfireServer((cfg) => {
				cfg.CancellationCheckInterval = TimeSpan.FromSeconds(5);
				cfg.Queues = new[] { config.UseLocalQueue ? System.Environment.MachineName.ToLower() :  (config.QueueName ?? "default") };
				if (config.WorkerCount != null) cfg.WorkerCount = config.WorkerCount.Value;
			});
			services.AddHostedService<JobManager>();
			services.AddSingleton<IJobConfig>(config);
			services.AddSingleton<IJobManager, JobManager>();
		}

		public void AddJob<TService, TImplementation>() where TImplementation : JobRunner, TService {
			_services.AddSingleton(typeof(TService), typeof(TImplementation));
		}

		public void AddCronJob<T>(Action<CronBuilder> cron) where T : JobRunner {
			string id = typeof(T).Name;
			CronBuilder bld = new CronBuilder();
			cron.Invoke(bld);


			RecurringJobOptions opt = new RecurringJobOptions();
			opt.TimeZone = TimeZoneInfo.Local;

			string queue =  _config.UseLocalQueue ? System.Environment.MachineName.ToLower() :  _config.QueueName ?? "default";
			RecurringJob.AddOrUpdate<T>(id, queue, (job) => job.Run(job.GetType().Name, null, default), bld.ToString(), opt );
		}


		public void UseDashboard(IApplicationBuilder app, string appName) {
			if (!_config.EnableDashboard) return;
			app.UseHangfireDashboard("/jobs", new DashboardOptions {
				DashboardTitle = _config.DbName ??  $"{appName} Jobs",
				AppPath = "/index.html",
				Authorization = new[] { new JobAuth() }
			}, _storage);
		}




	}
}
