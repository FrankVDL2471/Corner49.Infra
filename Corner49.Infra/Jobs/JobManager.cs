using Hangfire;
using Hangfire.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Corner49.Infra.Jobs {

	public interface IJobManager {

		string StartJob<T>(Dictionary<string, string>? args = null, string? queueName = null) where T : IJobRunner;

		string GetJobStatus(string jobId);

		IEnumerable<JobInfo> GetJobs();
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

		public string StartJob<T>(Dictionary<string, string>? args = null,string? queue = null) where T : IJobRunner {
			var nm = _config.UseLocalQueue ? System.Environment.MachineName.ToLower() :  queue ?? _config.QueueName ?? "default";
			try {

				var job = ActivatorUtilities.CreateInstance<T>(_serviceProvider) as IJobRunner;
				if (job == null) {
					_logger.LogError($"StartJob {typeof(T).Name} failed : Unable to create instance");
					return "ERROR: Unable to create job instance";
				}


				return job.StartJob(args, nm);
			} catch (Exception err) {
				_logger.LogError(err, $"StartJob {typeof(T).Name} failed : {err.Message}");
				return "ERROR: StartJob failed, " + err.Message;
			}
		}

		public IEnumerable<JobInfo> GetJobs() {
			var api = JobStorage.Current.GetMonitoringApi();

			//foreach (var queue in api.Queues()) {
			//	foreach (var job in api.EnqueuedJobs(queue.Name, 0, (int)api.EnqueuedCount(queue.Name))) {
			//		if (job.Value?.InvocationData == null) continue;
			//		var flds = JsonSerializer.Deserialize<string[]>(job.Value.InvocationData.Arguments);

			//		yield return new JobInfo {
			//			Id = job.Key,
			//			Type = job.Value.InvocationData.Type,
			//			Status = "Pending",
			//			Name = flds?.Length > 0 ? flds[0].TrimStart('"').TrimEnd('"') : null,
			//			Args = flds?.Length > 1 ? JsonSerializer.Deserialize<Dictionary<string, string>>(flds[1]) : null
			//		};
			//	}
			//}

			foreach (var job in api.ProcessingJobs(0, (int)api.ProcessingCount())) {
				var flds = JsonSerializer.Deserialize<string[]>(job.Value.InvocationData.Arguments);

				yield return new JobInfo {
					Id = job.Key,
					Type = job.Value.InvocationData.Type,
					Status = job.Value.InProcessingState ? "Processing" : "Pending",
					Name = ((flds != null) && (flds?.Length > 0) && (flds[0] != null)) ? flds[0].TrimStart('"').TrimEnd('"') : null,
					Args = ((flds != null) && (flds?.Length > 1) && (flds[1] != null)) ? JsonSerializer.Deserialize<Dictionary<string, string>>(flds[1]) : null
				};
			}
			foreach (var job in api.FailedJobs(0, (int)api.FailedCount())) {
				if (job.Value?.InvocationData == null) continue;
				var flds = JsonSerializer.Deserialize<string[]>(job.Value.InvocationData.Arguments);

				yield return new JobInfo {
					Id = job.Key,
					Type = job.Value.InvocationData.Type,
					Status = "Failed",
					Name = ((flds != null) && (flds?.Length > 0) && (flds[0] != null)) ? flds[0].TrimStart('"').TrimEnd('"') : null,
					Args = ((flds != null) && (flds?.Length > 1) && (flds[1] != null)) ? JsonSerializer.Deserialize<Dictionary<string, string>>(flds[1]) : null
				};
			}



		}


		public string GetJobStatus(string jobId) {
			IStorageConnection connection = JobStorage.Current.GetConnection();
			JobData jobData = connection.GetJobData(jobId);
			return jobData.State;




		}



	}
}
