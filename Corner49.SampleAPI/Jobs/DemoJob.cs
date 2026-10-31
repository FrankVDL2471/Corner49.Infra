using Apache.Arrow;
using Corner49.Infra.Jobs;

namespace Corner49.SampleAPI.Jobs {
	public class DemoJob : JobRunner {

		private readonly ILogger<DemoJob> _logger;

		public DemoJob(ILogger<DemoJob> logger, IServiceProvider serviceProvider) : base(serviceProvider) {
			_logger = logger;	
		}

		public override async Task Execute(Dictionary<string, string>? args = null, CancellationToken cancellationToken = default) {
			_logger.LogInformation("Demo job running");
			await Task.Delay(TimeSpan.FromMinutes(5));
			_logger.LogInformation("Demo job done");
		}
	}
}
