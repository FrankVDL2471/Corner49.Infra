using Corner49.Infra.ServiceBus;
using Microsoft.Extensions.DependencyInjection;

namespace Corner49.Infra.Messages {
	public static class ServiceExtensions {

		public static InfraBuilder AddMessages(this InfraBuilder infra) {
			infra.Services.AddSingleton<IServiceBusService, ServiceBusService>();
			return infra;
		}

		public static IServiceCollection AddMessages(this IServiceCollection services) {
			services.AddSingleton<IServiceBusService, ServiceBusService>();
			return services;
		}



		public static InfraBuilder AddMessageHandler<T, H>(this InfraBuilder infra, int? maxConcurrentCalls = null) where T : MessageBase where H : MessageHandler<T> {
			var msg = Activator.CreateInstance<T>();
			infra.AddServiceBusHandler<H>((opt) => {
				opt.Name = msg.Name;
				if (msg.UseQueue) {
					opt.Kind = ServiceBusKind.Queue;
				} else {
					opt.Kind = ServiceBusKind.Topic;
					opt.SubscriptionName = infra.Name + "." + typeof(T).Name;
				}
#if DEBUG
				opt.MaxConcurrentCalls = 1;
#else
                opt.MaxConcurrentCalls = maxConcurrentCalls ?? 10;
#endif
				opt.DuplicateDetectionWindow = TimeSpan.FromSeconds(30);
			});

			return infra;
		}





	}
}
