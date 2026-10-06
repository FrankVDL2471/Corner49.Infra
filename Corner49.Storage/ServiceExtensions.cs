using Corner49.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Corner49.Storage {
	public static class ServiceExtensions {

		public static void AddBlobService(this IInfraBuilder infra, string name) {
			infra.Services.AddBlobService(name);
		}

		public static void AddBlobService(this IServiceCollection services, string name) {
			services.AddKeyedScoped<IBlobService>(name, (p, o) => { return new BlobService(name, p.GetRequiredService<IConfiguration>()); });
		}

		public static IBlobService? GetBlobService(this IServiceProvider serviceProvider, string name) {
			return serviceProvider.GetRequiredKeyedService<IBlobService>(name);
		}

	}
}
