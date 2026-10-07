using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Corner49.Infra {
	public interface IInfraBuilder {

		string Name { get; }

		IServiceCollection Services { get; }
		IConfigurationManager Configuration { get; }

		IInfraBuilder AddExtension(InfraExtension extension);
	}
}
