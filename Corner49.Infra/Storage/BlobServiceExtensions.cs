using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Corner49.Infra.Storage {
	public static class BlobServiceExtensions  {


		public static void AddBlobService(this IServiceCollection services, string name) {
			services.AddKeyedScoped<IBlobService>(name, (p, o) => { return new BlobService(name, p.GetRequiredService<IConfiguration>() ); });
		}

		public static IBlobService? GetBlobService(this IServiceProvider serviceProvider, string name) {
			return serviceProvider.GetRequiredKeyedService<IBlobService>(name);
		}

	}
}
