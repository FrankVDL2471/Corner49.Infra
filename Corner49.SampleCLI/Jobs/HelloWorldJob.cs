using Corner49.Infra.Jobs;

namespace Corner49.SampleCLI.Jobs {
	internal class HelloWorldJob : IJob {

		public HelloWorldJob(IServiceProvider services) {

		}

		public async Task Execute(string[]? args = null, CancellationToken cancellationToken = default) {
			Console.WriteLine("Hello World!");
		}
	}
}
