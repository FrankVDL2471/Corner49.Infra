using Hangfire;
using Hangfire.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

namespace Corner49.Infra.Jobs {

	public interface IJobRunner {

		void StartJob(Dictionary<string, string>? args = null, bool useLocalQueue = false, CancellationToken cancellationToken = default);
	}

	public abstract class JobRunner : IJobRunner {

		private readonly IServiceProvider _serviceProvider;
		private readonly ILogger<JobRunner> _logger;
		private readonly IBackgroundJobClient _jobClient;



		public JobRunner(IServiceProvider serviceProvider) {
			_serviceProvider = serviceProvider;
			_logger = serviceProvider.GetRequiredService<ILogger<JobRunner>>();
			_jobClient = _serviceProvider.GetRequiredService<IBackgroundJobClient>();
		}

		public IServiceProvider Services => _serviceProvider;

		public void StartJob(Dictionary<string, string>? args = null, bool useLocalQueue = false, CancellationToken cancellationToken = default) {
			string name = GetType().Name;

			if (args != null) {
				name += "(" + string.Join(", ", args.Select(c => $"{c.Key}={c.Value}")) + ")";
			}

			try {
				if (useLocalQueue) {
					_jobClient.Enqueue(() => RunLocal(name, args, cancellationToken));
				} else {
					_jobClient.Enqueue(() => Run(name, args, cancellationToken));
				}
			} catch (Exception err) {
				_logger.LogError(err, $"StartJob {name} failed : {err.Message}");
			}
		}


		[DisplayName("{0}")]
		[AutomaticRetry(OnAttemptsExceeded = AttemptsExceededAction.Delete, Attempts = 3)]
		public async Task Run(string name, Dictionary<string, string>? args = null, CancellationToken cancellationToken = default) {
			string fullName = name;
			if (args != null) {
				fullName += "(" + string.Join(", ", args.Select(c => $"{c.Key}={c.Value}")) + ")";
			}


			try {
				await Execute(args, cancellationToken);
			} catch(Exception err) {
				_logger.LogError(err, $"Job {fullName} failed : {err.Message}");
			}
		}

		[DisplayName("{0}")]
		[JobQueue()]
		[AutomaticRetry(OnAttemptsExceeded = AttemptsExceededAction.Delete, Attempts = 3)]
		public async Task RunLocal(string name, Dictionary<string, string>? args = null, CancellationToken cancellationToken = default) {
			string fullName = name;
			if (args != null) {
				fullName += "(" + string.Join(", ", args.Select(c => $"{c.Key}={c.Value}")) + ")";
			}


			try {
				await Execute(args, cancellationToken);
			} catch (Exception err) {
				_logger.LogError(err, $"Job {fullName} failed : {err.Message}");
			}
		}




		public abstract Task Execute(Dictionary<string, string>? args = null, CancellationToken cancellationToken = default);



		private IEnumerable<string> GetJobKeys(string name) {

			var monitor = JobStorage.Current.GetMonitoringApi();


			//Kill jobs in processing
			int skip = 0;
			while (true) {
				var jobs = monitor.ProcessingJobs(skip, 100);				
				foreach (var job in jobs) {					
					if (job.Value.Job.Args.Count < 1) continue;
					if ((string)job.Value.Job.Args[0] != name) continue;

					yield return job.Key; 
				}
				if (jobs.Count < 100) break;
				skip += 100;
			}

			//Remove alreayd queued jobs
			foreach (var queue in monitor.Queues()) {
				skip = 0;
				while (true) {
					var jobs = monitor.EnqueuedJobs(queue.Name, skip, 100);
					foreach (var job in jobs) {
						if (job.Value.Job.Args.Count < 1) continue;
						if ((string)job.Value.Job.Args[0] != name) continue;

						yield return job.Key;
					}
					if (jobs.Count < 100) break;
					skip += 100;
				}
			}


		}



	}
}
