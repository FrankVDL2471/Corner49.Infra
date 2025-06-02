using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Corner49.Infra.DB {
	public static class DocumentDBExtensions {

		public static DocumentDBBuilder AddDocumentDB(this IServiceCollection services, IConfiguration config,   Action<DocumentDBBuilder>? repos = null) {

			services.AddSingleton<IDocumentDB, DocumentDB>();

			var docDBBuilder = new DocumentDBBuilder(services);
			if (repos != null) {
				repos(docDBBuilder);
			}


			services.Configure<DocumentDBOptions>((cfg) => {
				config.GetSection(DocumentDBOptions.SectionName).Bind(cfg);
				if (docDBBuilder.Configure != null) {
					docDBBuilder.Configure(cfg);
				}
			});
			return docDBBuilder;
		}

	}
}
