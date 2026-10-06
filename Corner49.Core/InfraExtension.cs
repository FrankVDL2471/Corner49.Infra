using Microsoft.Extensions.Configuration;

namespace Corner49.Core {
	public class InfraExtension {




		public virtual Task Build(IInfraBuilder ìnfra, IConfiguration config) {
			return Task.CompletedTask;
		}


		public virtual Task Start(IServiceProvider serviceProvider) {
			return Task.CompletedTask;
		}

	}
}
