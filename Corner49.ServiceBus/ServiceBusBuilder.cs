using Corner49.ServiceBus.Bus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Corner49.ServiceBus {
	public class ServiceBusBuilder {

		private IServiceCollection _services;

		internal ServiceBusBuilder(IServiceCollection services) {
			_services = services;	
		}


		public void AddServiceBusHandler<T>(Action<IServiceBusOptions>? options = null) where T : class, IServiceBusHandler {
			_services.AddHostedService((srv) => {
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

	}
}
