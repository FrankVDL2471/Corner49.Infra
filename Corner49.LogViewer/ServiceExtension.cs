using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;

namespace Corner49.LogViewer {
	public static class ServiceExtension {

		public static void AddLogViewer(this IServiceCollection services) {
			var assembly = typeof(Controllers.LogViewerController).Assembly;

			var part = new AssemblyPart(assembly);
			services.AddControllersWithViews()
					.ConfigureApplicationPartManager(apm => apm.ApplicationParts.Add(part));
		}

		public static IMvcBuilder AddLogViewer(this IMvcBuilder builder) {
			var assembly = typeof(Controllers.LogViewerController).Assembly;

			var part = new AssemblyPart(assembly);
			return builder.ConfigureApplicationPartManager(apm => apm.ApplicationParts.Add(part));
		}

	}
}
