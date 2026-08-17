using Microsoft.Extensions.DependencyInjection;

namespace Corner49.Infra.DB {


	public class DocumentRepoOptions {

		/// <summary>
		/// Override the default database name for this Repo
		/// </summary>
		public string? DatabaseName { get; set; }
		public string? ContainerName { get; set; }

		/// <summary>
		/// Autoscale max RU/s to provision on the database if Init() creates it. Null (default) creates the
		/// database with no explicit throughput. Apply via the repo's <c>DatabaseAutoscaleThroughput</c> property.
		/// </summary>
		public int? DatabaseAutoscaleThroughput { get; set; }

		/// <summary>
		/// Autoscale max RU/s to provision on the container if Init() creates it. Null (default) creates the
		/// container with no explicit throughput - on a non-serverless account this can leave the container
		/// with no RU/s budget, a common root cause of 429 TooManyRequests errors. Apply via the repo's
		/// <c>ContainerAutoscaleThroughput</c> property.
		/// </summary>
		public int? ContainerAutoscaleThroughput { get; set; }
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
					return ActivatorUtilities.CreateInstance<T>(srv, db, ops);
				});
			}
			_repoTypes.Add(typeof(T), ops);
		}


		public async Task Init(IServiceProvider serviceProvider) {
			foreach (var tp in _repoTypes) {
				var repo = (tp.Value == null ? ActivatorUtilities.CreateInstance(serviceProvider, tp.Key) : ActivatorUtilities.CreateInstance(serviceProvider, tp.Key, tp.Value)) as IDocumentRepoInitializer;
				if (repo != null) {
					await repo.Init();
				}
			}
		}

	}
}
