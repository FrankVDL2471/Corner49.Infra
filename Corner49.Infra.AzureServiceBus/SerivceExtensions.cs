using Corner49.Infra.Messages;
using Corner49.Infra.ServiceBus;

namespace Corner49.Infra {
	public static class SerivceExtensions {


		public static IInfraBuilder AddServiceBus(this IInfraBuilder infra, Action<ServiceBusConfiguration> config, Action<ServiceBusBuilder>? build = null) {
			var ext = new ServiceBusExtension(config);
			ext.Init = build;
			return infra.AddExtension(ext);
		}


		public static void AddServiceBusHandler<T>(this IInfraBuilder infra, Action<IServiceBusOptions>? options = null) where T : class, IServiceBusHandler { 
			var ext = new ServiceBusExtension();
			ext.Init = (bld) => {
				bld.AddServiceBusHandler<T>(options);
			};
			infra.AddExtension(ext);
		}

		public static void AddMessageHandler<T, H>(this IInfraBuilder infra, int? maxConcurrentCalls = null) where T : MessageBase where H : MessageHandler<T> {
			var ext = new ServiceBusExtension();
			ext.Init = (bld) => {
				bld.AddMessageHandler<T, H>(maxConcurrentCalls);
			};
			infra.AddExtension(ext);
		}
	}
}
