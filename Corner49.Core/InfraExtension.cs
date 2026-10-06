using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Corner49.Core {
	public class InfraExtension {




		public virtual Task Build(IServiceCollection services, IConfiguration config) {
			return Task.CompletedTask;
		}


		public virtual Task Start(IServiceProvider serviceProvider) {
			return Task.CompletedTask;
		}

	}
}
