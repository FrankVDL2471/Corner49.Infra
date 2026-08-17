using Corner49.Infra.Tools;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Corner49.Infra.DB {

	public interface IDocumentDB {


		DocumentRepo<T> GetRepo<T>(string dbName, string tblName, params string[] paritionKey) where T : class;
		DocumentRepo<T> GetRepo<T>(string tblName, params string[] paritionKey) where T : class;

	}

	public class DocumentDB : IDocumentDB {

		// Static cache to ensure CosmosClient instances are reused (singleton pattern)
		private static readonly ConcurrentDictionary<string, CosmosClient> _clients = new ConcurrentDictionary<string, CosmosClient>();

		private CosmosClient _client = null!;
		private readonly DocumentDBOptions _options;

		public DocumentDB(IOptions<DocumentDBOptions> options) {
			_options = options.Value;
			this.Connect();
		}
		public DocumentDB(string connectString, string dbName = "data", bool directMode = false) {
			_options = new DocumentDBOptions();
			_options.ConnectString = connectString;
			_options.DatabaseName = dbName;
			_options.DirectMode = directMode;

			this.Connect();
		}




		private void Connect() {
			// Create a unique cache key based on connection string and configuration
			string cacheKey = $"{_options.ConnectString}|{_options.DirectMode}";

			// Reuse existing client if available, otherwise create new one
			_client = _clients.GetOrAdd(cacheKey, key => {
				CosmosClientOptions options = new CosmosClientOptions();
				options.UseSystemTextJsonSerializerWithOptions = JsonHelper.Options;
				options.ConnectionMode = _options.DirectMode ? ConnectionMode.Direct : ConnectionMode.Gateway;

				if (options.ConnectionMode == ConnectionMode.Direct) {
					options.IdleTcpConnectionTimeout = TimeSpan.FromHours(1);
				}
				options.AllowBulkExecution = true;

				// DocumentRepo<T> already runs its own bounded, RetryAfter-aware retry loop on 429/408 for
				// every operation (surfacing attempts via OnDiagnostics), so the SDK's own retry budget is
				// kept small rather than left at its default (9 attempts / 30s) to avoid two uncoordinated
				// retry layers stacking into very long worst-case latency under sustained throttling.
				// This still leaves a small safety net for code paths that don't have their own retry loop
				// (e.g. BulkInsert/BulkUpdate/BulkDelete).
				options.MaxRetryAttemptsOnRateLimitedRequests = 3;
				options.MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(15);

				return new CosmosClient(_options.ConnectString, options);
			});
		}


		public DocumentRepo<T> GetRepo<T>(string dbName, string tblName, params string[] paritionKey) where T : class {
			return new DocumentRepo<T>(_client, dbName, tblName, paritionKey);
		}


		public DocumentRepo<T> GetRepo<T>(string tblName, params string[] paritionKey) where T : class {
			return new DocumentRepo<T>(_client, _options.DatabaseName, tblName, paritionKey);
		}





	}
}
