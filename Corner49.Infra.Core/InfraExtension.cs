using Microsoft.Extensions.Configuration;

namespace Corner49.Infra {
	public class InfraExtension {




		public virtual Task Build(IInfraBuilder ìnfra, IConfiguration config) {
			return Task.CompletedTask;
		}


		public virtual Task Start(IServiceProvider serviceProvider, object? host = null) {
			return Task.CompletedTask;
		}

	}
}
