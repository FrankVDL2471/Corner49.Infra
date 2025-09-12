using Corner49.Infra.Jobs;

namespace Corner49.Sample.Jobs {
	public class TestJob : JobRunner {


		public TestJob(IServiceProvider provider) : base(provider) { 
		}
		
		public override Task Execute(Dictionary<string, string>? args = null, CancellationToken cancellationToken = default) {
			Console.WriteLine($"Run TestJob : {DateTimeOffset.Now}");
			return Task.CompletedTask;
		}
	}
}
