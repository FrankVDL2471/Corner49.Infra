using Corner49.Infra.ServiceBus;

namespace Corner49.Infra {
	public static class SerivceExtensions {


		public static IInfraBuilder AddServiceBus(this IInfraBuilder infra, Action<ServiceBusConfiguration> config, Action<ServiceBusBuilder>? build = null) {
			var ext = new ServiceBusExtension(config);
			ext.Init = build;
			return infra.AddExtension(ext);
		}

	}
}
