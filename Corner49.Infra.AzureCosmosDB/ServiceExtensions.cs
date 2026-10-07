using Corner49.Infra.DB;

namespace Corner49.Infra {
	public static class ServiceExtensions {


		public static IInfraBuilder AddDocumentDB(this IInfraBuilder infra, Action<DocumentDBBuilder>? repos = null) {
			return infra.AddExtension(new DocumentDBExtension(repos));
		}

	}
}
