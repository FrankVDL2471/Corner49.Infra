using Corner49.Core;
using Corner49.ServiceBus.Bus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Corner49.ServiceBus {
	public class ServiceBusExtension : InfraExtension {


		private Action<ServiceBusConfiguration>? _config;

		public ServiceBusExtension(Action<ServiceBusConfiguration>? config = null) {

			_config = config;
		}


		public Action<ServiceBusBuilder>? Init { get; set; }
		

		public override Task Build(IInfraBuilder infra, IConfiguration config) {
			infra.Services.Configure<ServiceBusConfiguration>((cfg) => {
				config.GetSection(ServiceBusConfiguration.SectionName).Bind(cfg);
				if (_config != null) {
					_config(cfg);
				}
			});
			infra.Services.AddSingleton<IServiceBusService, ServiceBusService>();


			
			if (this.Init != null) {
				var bld = new ServiceBusBuilder(infra); 
				this.Init(bld);
			}


			return base.Build(infra, config);
		}

	}
}
