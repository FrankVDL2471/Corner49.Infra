using Corner49.Core;
using Corner49.ServiceBus.Bus;

namespace Corner49.ServiceBus {
	public static class SerivceExtensions {


		public static IInfraBuilder AddServiceBus(this IInfraBuilder infra, Action<ServiceBusConfiguration> config, Action<ServiceBusBuilder>? build = null) {
			var ext = new ServiceBusExtension(config);
			ext.Init = build;
			return infra.AddExtension(ext);
		}

	}
}
