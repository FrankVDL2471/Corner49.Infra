using Corner49.Core;
using Corner49.CosmosDB.DB;

namespace Corner49.CosmosDB {
	public static class ServiceExtensions {


		public static IInfraBuilder AddDocumentDB(this IInfraBuilder infra, Action<DocumentDBBuilder>? repos = null) {
			return infra.AddExtension(new DocumentDBExtension(repos));
		}

	}
}
