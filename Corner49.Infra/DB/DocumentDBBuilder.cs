using Microsoft.Extensions.DependencyInjection;
using System.Configuration.Internal;

namespace Corner49.Infra.DB {


	public class DocumentRepoOptions {

		/// <summary>
		/// Override the default database name for this Repo
		/// </summary>
		public string? DatabaseName { get; set; }
		public string? ContainerName { get; set; }
	}
	public class DocumentDBBuilder {

		private readonly IServiceCollection _services;
		private readonly Dictionary<Type, DocumentRepoOptions?> _repoTypes;

		public DocumentDBBuilder(IServiceCollection services) {
			_services = services;
			_repoTypes = new Dictionary<Type, DocumentRepoOptions?>();
		}

		public Action<DocumentDBOptions> Configure { get; set; }	

		public void AddRepo<I, T>(Action<DocumentRepoOptions>? options = null) where I : class where T : class, I, IDocumentRepoInitializer {
			DocumentRepoOptions? ops = null;
			if (options == null) {
				_services.AddSingleton<I, T>();	
			} else {
				ops = new DocumentRepoOptions();
				options(ops);

				_services.AddSingleton<I, T>((srv) => {
					var db = srv.GetServices<IDocumentDB>();
					return ActivatorUtilities.CreateInstance<T>(srv, db,ops);
				});
			}
			_repoTypes.Add(typeof(T), ops);
		}


		public async Task Init(IServiceProvider serviceProvider) {
			foreach (var tp in _repoTypes) {
				var repo = (tp.Value == null ? ActivatorUtilities.CreateInstance(serviceProvider, tp.Key)  : ActivatorUtilities.CreateInstance(serviceProvider, tp.Key, tp.Value))as IDocumentRepoInitializer;
				if (repo != null) {
					await repo.Init();
				}
			}
		}

	}
}
