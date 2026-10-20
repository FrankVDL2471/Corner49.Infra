using Corner49.Infra.Jobs;
using Corner49.Infra.ServiceBus;
using Microsoft.Extensions.DependencyInjection;

namespace Corner49.Infra {
	public abstract class ServiceBusJob : IJob {


		private readonly IServiceBusService _serviceBus;

		public ServiceBusJob(IServiceProvider services) {
			_serviceBus = services.GetRequiredService<IServiceBusService>();
		}

		public string? Queue { get; set; }

		public string? Topic { get; set; }
		public string? Subscription { get; set; }	
		public int PrefetchCount { get; set; } = 0;


		public async Task Execute(string[]? args = null, CancellationToken cancellationToken = default) {


			ServiceBusOptions options = new ServiceBusOptions(this.Subscription ?? string.Empty);
			if (this.Queue != null) {
				options.Kind = ServiceBusKind.Queue;
				options.Name = this.Queue;
			} else if (this.Topic != null) {
				options.Kind = ServiceBusKind.Topic;
				options.Name = this.Topic;

				if (options.SubscriptionName == null) {
					throw new ArgumentException("Subscription must be specified for a topic.");
				}
			} else {
				throw new ArgumentException("Queue or Topic must be specified.");
			}

			options.PrefetchCount = this.PrefetchCount;

			await _serviceBus.ReadMessages(options, (cmd) => {
				return this.Run(cmd, cancellationToken);
			});
		}

		
		protected abstract Task<bool> Run(ServiceBusCommand command, CancellationToken cancellationToken = default);
	}
}
