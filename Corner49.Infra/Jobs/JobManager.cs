﻿using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Corner49.Infra.Jobs {

	public interface IJobManager {

		void StartJob<T>(Dictionary<string, string>? args = null, bool? useLocalQueue = null) where T : IJobRunner;

	}

	public class JobManager : IHostedService, IJobManager {

		private readonly ILogger<JobManager> _logger;
		private readonly IServiceProvider _serviceProvider;
		private readonly IBackgroundJobClient _jobClient;
		private readonly IJobConfig _config;

		public JobManager(ILogger<JobManager> logger, IServiceProvider serviceProvider, IBackgroundJobClient jobClient, IJobConfig config	) {
			_logger = logger;
			_serviceProvider = serviceProvider;
			_jobClient = jobClient;
			_config = config;
		}

		public Task StartAsync(CancellationToken cancellationToken) {
			//Automatic reque jobs that are aborted by a restart of the appservice (new deployment)
			try {
				if (!_config.DisableAutomaticRestart) {
					var api = JobStorage.Current.GetMonitoringApi();
					var processingJobs = api.ProcessingJobs(0, 100);
					var servers = api.Servers();
					var orphanJobs = processingJobs.Where(j => !servers.Any(s => s.Name == j.Value?.ServerId));
					foreach (var orphanJob in orphanJobs) {
						_logger.LogInformation($"Requeue job {orphanJob.Key}");
						_jobClient.Requeue(orphanJob.Key);
					}
				}

			} catch (Exception err) {
				_logger.LogError(err, $"Requeue jobs failed : {err.Message}");
			}
			return Task.CompletedTask;
		}

		public Task StopAsync(CancellationToken cancellationToken) {
			return Task.CompletedTask;
		}

		public void StartJob<T>(Dictionary<string, string>? args = null, bool? useLocalQueue = null) where T : IJobRunner {
			var localQueue = useLocalQueue ?? _config.UseLocalQueue;


			var job = ActivatorUtilities.CreateInstance<T>(_serviceProvider) as IJobRunner;
			job.StartJob(args, localQueue);
		}
	}
}
