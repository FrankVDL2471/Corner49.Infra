using Corner49.Infra.Messages;
using Corner49.Infra.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Corner49.Infra {
	public class ServiceBusBuilder {

		private IInfraBuilder _infra;

		internal ServiceBusBuilder(IInfraBuilder infra) {
			_infra = infra;
		}


		public void AddServiceBusHandler<T>(Action<IServiceBusOptions>? options = null) where T : class, IServiceBusHandler {
			_infra.Services.AddHostedService((srv) => {
				var logger = srv.GetRequiredService<ILogger<T>>();
				var config = srv.GetRequiredService<IConfiguration>();
				var bus = srv.GetRequiredService<IServiceBusService>();

				ServiceBusOptions opt = new ServiceBusOptions(typeof(T).Name);
				if (options != null) options.Invoke(opt);

				if (string.IsNullOrEmpty(opt.Name)) {
					throw new ArgumentNullException("Name", "ServiceBusOptions.Name must be set");
				}

				return new ServiceBusTrigger<T>(logger, srv, bus, opt);
			});
		}


		public void AddMessageService<T, S>() where T : MessageBase where S : MessageService<T> {
			_infra.Services.AddSingleton<IMessageService<T>, S>();
		}

		public void AddMessageHandler<T, H>(int? maxConcurrentCalls = null) where T : MessageBase where H : MessageHandler<T> {
			var msg = Activator.CreateInstance<T>();
			this.AddServiceBusHandler<H>((opt) => {
				opt.Name = msg.Name;
				if (msg.UseQueue) {
					opt.Kind = ServiceBusKind.Queue;
				} else {
					opt.Kind = ServiceBusKind.Topic;
					opt.SubscriptionName = _infra.Name + "." + typeof(T).Name;
				}
#if DEBUG
				opt.MaxConcurrentCalls = 1;
#else
                opt.MaxConcurrentCalls = maxConcurrentCalls ?? 10;
#endif
				opt.DuplicateDetectionWindow = TimeSpan.FromSeconds(30);
			});
		}




	}
}
