using Auth0.AspNetCore.Authentication;
using Corner49.DB.Tools;
using Corner49.Infra.Tools;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Corner49.Infra.DB {



	public interface IDocumentRepo<T> where T : class {
		Task<T?> GetItem(string paritionId, string itemId);

		IAsyncEnumerable<T> GetItems(string? paritionId);
		Task<T> AddItem(string paritionId, T item);

		Task<T> UpsertItem(string paritionId, T item, Action<HttpStatusCode>? status = null);

		Task<T> PatchItem(string partitionId, string itemId, IReadOnlyList<PatchOperation> patches);

		Task<bool> DeleteItem(string paritionId, string itemId);


		IQueryable<T> CreateQuery(string? paritionKey = null, int? maxItemCount = null);


		Task Read(string? partitionKey, Func<IQueryable<T>, IQueryable<T>> query, Func<T, int?, Task<bool>> onRead, int? maxItemCount = null, CancellationToken cancelToken = default);
		IAsyncEnumerable<T> GetQueryResults(IQueryable<T> qry, CancellationToken cancelToken = default);

		Task<QueryResult<T>> Filter(string? partitionKey, DocumentFilter<T> filter, CancellationToken cancelToken = default);
		Task<QueryResult<T>> Query(string? partitionKey, Func<IQueryable<T>, IQueryable<T>> query, string? continuationToken = null, int? maxItemCount = null, CancellationToken cancelToken = default);

		Task<QueryResult<T>> Query(string? partitionKey, string sql, string? continuationToken = null, int? maxItemCount = null, CancellationToken cancelToken = default);

		IAsyncEnumerable<M> ExecSQL<M>(string sql, CancellationToken cancelToken = default);
		Task<string?> ReadSQL(string sql, Func<T, Task<bool>> onRead, string? partitionKey = null, string? continuationToken = null, int? maxItemCount = null, CancellationToken cancelToken = default);
		Task<int> CountSQL(string where, System.Threading.CancellationToken cancelToken = default);

		IAsyncEnumerable<JsonElement> RawSQL(string sql, System.Threading.CancellationToken cancelToken = default);

		Task<ChangeFeedProcessor> GetChangeFeedProcessor(Container.ChangesHandler<T> changeHandler, string leaseName = "changeLeases", string? processorName = null, string? instanceName = null,
						Func<Task>? onRelease = null,
						Func<Exception, Task>? onError = null);

		Task<ChangeFeedProcessor> GetAllChangesFeedProcessor(Container.ChangeFeedHandler<T> changeHandler, string leaseName = "changeLeases", string? processorName = null, string? instanceName = null,
				Func<Task>? onRelease = null,
				Func<Exception, Task>? onError = null);
	}

	public interface IDocumentRepoInitializer {

		Task Init();
	}

	public class DocumentRepo<T> : IDocumentRepoInitializer, IDocumentRepo<T> where T : class {

		//		private readonly IConfiguration _config;

		private readonly CosmosClient _client;

		private readonly string _dbName;
		private readonly string _containerName;
		private readonly string _paritionKey;
		private readonly string _itemKey;


		public DocumentRepo(CosmosClient client, string dbName, string containerName, string paritionKey, string itemKey = "id") {
			_client = client;

			_dbName = dbName;
			_containerName = containerName;
			_paritionKey = paritionKey;
			_itemKey = itemKey;
		}


		private Database _database;
		private Container _container;

		protected Container Container {
			get {
				if (_database == null) _database = _client.GetDatabase(_dbName);
				if (_container == null) _container = _database.GetContainer(_containerName);
				return _container;
			}
		}

		public async Task Init() {
			var dbResp = await _client.CreateDatabaseIfNotExistsAsync(_dbName);
			if (dbResp.Database == null) {
				throw new DocumentException($"Database '{_dbName}' not created");
			}

			var resp = await dbResp.Database.DefineContainer(_containerName, "/" + _paritionKey)
				.WithDefaultTimeToLive(-1)
				.CreateIfNotExistsAsync();
		}


		public async Task<ChangeFeedProcessor> GetChangeFeedProcessor(Container.ChangesHandler<T> changeHandler, string leaseName = "changeLeases", string? processorName = null, string? instanceName = null,
						Func<Task>? onRelease = null,
						Func<Exception, Task>? onError = null) {

			if (this.Container == null) throw new DocumentContainerNotFoundException(_containerName);

			var resp = await _database.CreateContainerIfNotExistsAsync(new ContainerProperties(leaseName, "/id"));
			var leaseContainer = resp.Container;

			string procName = processorName ?? "proc_" + _containerName;
			string instName = instanceName ?? "inst_" + _containerName;

			var bld = this.Container.GetChangeFeedProcessorBuilder(processorName: procName, changeHandler)
					 .WithInstanceName(instName)
					 .WithLeaseContainer(leaseContainer);


			if (onRelease != null) bld = bld.WithLeaseReleaseNotification((lease) => { return onRelease(); });
			if (onError != null) bld = bld.WithErrorNotification((lease, err) => { return onError(err); });

			return bld.Build();
		}

		public async Task<ChangeFeedProcessor> GetAllChangesFeedProcessor(Container.ChangeFeedHandler<T> changeHandler, string leaseName = "changeLeases", string? processorName = null, string? instanceName = null,
				Func<Task>? onRelease = null,
				Func<Exception, Task>? onError = null) {

			if (this.Container == null) throw new DocumentContainerNotFoundException(_containerName);

			var resp = await _database.CreateContainerIfNotExistsAsync(new ContainerProperties(leaseName, "/id"));
			var leaseContainer = resp.Container;

			string procName = processorName ?? "proc_" + _containerName;
			string instName = instanceName ?? "inst_" + _containerName;


			var bld = this.Container.GetChangeFeedProcessorBuilder<T>(procName, changeHandler)
					 .WithInstanceName(instName)
					 .WithLeaseContainer(leaseContainer);


			if (onRelease != null) bld = bld.WithLeaseReleaseNotification((lease) => { return onRelease(); });
			if (onError != null) bld = bld.WithErrorNotification((lease, err) => { return onError(err); });

			return bld.Build();
		}




		public async Task<T?> GetItem(string partitionId, string itemId) {
			if (this.Container == null) throw new DocumentContainerNotFoundException(_containerName);

			Exception? lastErr = null;
			for (int retry = 0; retry <= 3; retry++) {
				try {
					var resp = await this.Container.ReadItemAsync<T>(itemId, new PartitionKey(partitionId));
					if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
					return resp.Resource;
				} catch (CosmosException err) {
					lastErr = err;
					if (err.StatusCode == System.Net.HttpStatusCode.NotFound) {
						return null;
					} else if (err.StatusCode == System.Net.HttpStatusCode.TooManyRequests) {
						await Task.Delay(err.RetryAfter ?? TimeSpan.FromSeconds(5));
					} else {
						throw new DocumentException($"GetItem({partitionId},{itemId}) failed", err);
					}
				} catch (Exception ex) {
					throw new DocumentException($"GetItem({partitionId},{itemId}) failed", ex);
				}
			}
			throw new DocumentException($"GetItem({partitionId},{itemId}) failed", lastErr);
		}

		public IAsyncEnumerable<T> GetItems(string? paritionId) {
			if (this.Container == null) throw new DocumentContainerNotFoundException(_containerName);


			return this.GetQueryResults(this.CreateQuery(paritionId));
		}

		public async Task<T> AddItem(string paritionId, T item) {
			if (this.Container == null) throw new DocumentContainerNotFoundException(_containerName);

			Exception? lastErr = null;
			for (int retry = 0; retry <= 3; retry++) {
				try {
					var resp = await this.Container.CreateItemAsync(item, new PartitionKey(paritionId));
					return resp.Resource;

				} catch (CosmosException err) {
					lastErr = err;
					if (err.StatusCode == System.Net.HttpStatusCode.TooManyRequests) {
						await Task.Delay(err.RetryAfter ?? TimeSpan.FromSeconds(5));
					} else {
						throw new DocumentException($"AddItem failed", err);
					}
				} catch (Exception ex) {
					throw new DocumentException($"AddItem failed", ex);
				}
			}
			throw new DocumentException($"AddItem failed", lastErr);
		}

		public async Task<T> UpsertItem(string partitionId, T item, Action<HttpStatusCode>? status = null) {
			if (this.Container == null) throw new DocumentContainerNotFoundException(_containerName);

			Exception? lastErr = null;
			for (int retry = 0; retry <= 3; retry++) {
				try {
					var resp = await this.Container.UpsertItemAsync(item, new PartitionKey(partitionId));
					if (status != null) status.Invoke(resp.StatusCode);
					return resp.Resource;

				} catch (CosmosException err) {
					lastErr = err;
					if (err.StatusCode == System.Net.HttpStatusCode.TooManyRequests) {
						await Task.Delay(err.RetryAfter ?? TimeSpan.FromSeconds(5));
					} else {
						throw new DocumentException($"UpsertItem failed", err);
					}
				} catch (Exception err) {
					throw new DocumentException($"UpsertItem failed", err);
				}
			}
			throw new DocumentException($"UpsertItem failed", lastErr);
		}

		public async Task<T> PatchItem(string partitionId, string itemId, IReadOnlyList<PatchOperation> patches) {
			if (this.Container == null) throw new DocumentContainerNotFoundException(_containerName);

			Exception? lastErr = null;
			for (int retry = 0; retry <= 3; retry++) {
				try {
					return await this.Container.PatchItemAsync<T>(itemId, new PartitionKey(partitionId), patches);
				} catch (CosmosException err) {
					lastErr = err;
					if (err.StatusCode == System.Net.HttpStatusCode.TooManyRequests) {
						await Task.Delay(err.RetryAfter ?? TimeSpan.FromSeconds(5));
					} else {
						throw new DocumentException($"PatchItem({partitionId},{itemId}) failed", err);
					}
				} catch (Exception err) {
					throw new DocumentException($"PatchItem({partitionId},{itemId}) failed", err);
				}
			}
			throw new DocumentException($"PatchItem({partitionId},{itemId}) failed", lastErr);

		}

		public async Task<bool> DeleteItem(string paritionId, string itemId) {
			if (this.Container == null) throw new DocumentContainerNotFoundException(_containerName);

			try {
				var resp = await this.Container.DeleteItemAsync<T>(itemId, new PartitionKey(paritionId));
				if (resp.StatusCode == System.Net.HttpStatusCode.NoContent) return true;
				if (resp.StatusCode == System.Net.HttpStatusCode.OK) return true;

				return false;
			} catch (Exception err) {
				return false;
			}
		}







		public IQueryable<T> CreateQuery(string? partitionKey = null, int? maxItemCount = null) {
			if (this.Container == null) throw new DocumentContainerNotFoundException(_containerName);

			QueryRequestOptions queryOptions = new QueryRequestOptions();
			if (partitionKey != null) queryOptions.PartitionKey = new PartitionKey(partitionKey);
			if (maxItemCount != null) queryOptions.MaxItemCount = maxItemCount!;

			return this.Container.GetItemLinqQueryable<T>(true, requestOptions: queryOptions);
		}



		public async Task<string?> Read(string? partitionKey, DocumentFilter<T> filter, Func<T, Task> onRead, CancellationToken cancelToken = default) {
			if (this.Container == null) throw new DocumentContainerNotFoundException(_containerName);

			QueryRequestOptions queryOptions = new QueryRequestOptions();
			if (partitionKey != null) queryOptions.PartitionKey = new PartitionKey(partitionKey);
			if (filter?.Take != null) queryOptions.MaxItemCount = filter?.Take!;




			IQueryable<T> qry = this.Container.GetItemLinqQueryable<T>(true, continuationToken: filter?.ContinuationToken, requestOptions: queryOptions);

			string? token = null;
			using (FeedIterator<T> FeedIterator = qry.ToFeedIterator()) {
				while (FeedIterator.HasMoreResults && !cancelToken.IsCancellationRequested) {
					var feed = await FeedIterator.ReadNextAsync(cancelToken);
					foreach (var item in feed) {
						await onRead(item);
					}
					token = feed.ContinuationToken;
				}
			}
			return token;
		}


		public Task<QueryResult<T>> Filter(string? partitionKey, DocumentFilter<T> filter, CancellationToken cancelToken = default) {
			return this.Query(partitionKey, (qry) => filter.Build(qry), filter.ContinuationToken, filter.Take, cancelToken);
		}

		public async Task<QueryResult<T>> Query(string? partitionKey, Func<IQueryable<T>, IQueryable<T>> query, string? continuationToken = null, int? maxItemCount = null, CancellationToken cancelToken = default) {
			if (this.Container == null) throw new DocumentContainerNotFoundException(_containerName);

			QueryRequestOptions queryOptions = new QueryRequestOptions();
			if (partitionKey != null) queryOptions.PartitionKey = new PartitionKey(partitionKey);
			queryOptions.MaxItemCount = maxItemCount ?? -1;



			string? token = Base64.Decode(continuationToken);
			IQueryable<T> qry = this.Container.GetItemLinqQueryable<T>(false, continuationToken: token, requestOptions: queryOptions);
			qry = query(qry);

			QueryResult<T> result = new QueryResult<T>();
			if (token == null) {
				result.TotalCount = await qry.CountAsync(cancelToken);
			}
			using (FeedIterator<T> FeedIterator = qry.ToFeedIterator()) {
				while (FeedIterator.HasMoreResults && !cancelToken.IsCancellationRequested) {
					var feed = await FeedIterator.ReadNextAsync(cancelToken);
					foreach (var item in feed) {
						result.Data.Add(item);
					}
					result.ContinuationToken = Base64.Encode(feed.ContinuationToken);

					if ((maxItemCount != null) && (result.Data.Count >= maxItemCount)) break;
				}
			}
			return result;
		}

		public async Task<QueryResult<T>> Query(string? partitionKey, string sql, string? continuationToken = null, int? maxItemCount = null, CancellationToken cancelToken = default) {
			if (this.Container == null) throw new DocumentContainerNotFoundException(_containerName);

			QueryRequestOptions queryOptions = new QueryRequestOptions();
			if (partitionKey != null) queryOptions.PartitionKey = new PartitionKey(partitionKey);
			queryOptions.MaxItemCount = maxItemCount ?? -1;


			string? token = Base64.Decode(continuationToken);
			QueryDefinition def = new QueryDefinition(sql);

			QueryResult<T> result = new QueryResult<T>();
			if (token == null) {
				int idx = sql.IndexOf("where", 0, StringComparison.OrdinalIgnoreCase);
				if (idx > 0) {
					result.TotalCount = await this.CountSQL(sql.Substring(idx + 5), cancelToken);
				} else {
					result.TotalCount = await this.CountSQL(null, cancelToken);
				}
			}


			using (FeedIterator<T> FeedIterator = this.Container.GetItemQueryIterator<T>(def, token, queryOptions)) {
				while (FeedIterator.HasMoreResults && !cancelToken.IsCancellationRequested) {
					var feed = await FeedIterator.ReadNextAsync(cancelToken);
					foreach (var item in feed) {
						result.Data.Add(item);
					}
					result.ContinuationToken = Base64.Encode(feed.ContinuationToken);

					if ((maxItemCount != null) && (result.Data.Count >= maxItemCount)) break;
				}
			}

			return result;
		}





		public async Task Read(string? partitionKey, Func<IQueryable<T>, IQueryable<T>> query, Func<T, int?, Task<bool>> onRead, int? maxItemCount = null, CancellationToken cancelToken = default) {
			if (this.Container == null) throw new DocumentContainerNotFoundException(_containerName);

			QueryRequestOptions queryOptions = new QueryRequestOptions();
			if (partitionKey != null) queryOptions.PartitionKey = new PartitionKey(partitionKey);
			queryOptions.MaxItemCount = maxItemCount;


			int? count = null;
			string? token = null;
			while (true) {
				if (cancelToken.IsCancellationRequested) return;

				IQueryable<T> qry = this.Container.GetItemLinqQueryable<T>(true, continuationToken: token, requestOptions: queryOptions);
				qry = query(qry);

				if (token == null) {
					count = await qry.CountAsync(cancelToken);
				}

				using (FeedIterator<T> FeedIterator = qry.ToFeedIterator()) {
					while (FeedIterator.HasMoreResults && !cancelToken.IsCancellationRequested) {
						var feed = await FeedIterator.ReadNextAsync(cancelToken);
						foreach (var item in feed) {
							if (!await onRead(item, count)) return;
						}
						token = feed.ContinuationToken;
					}
				}
			}
		}






		public async IAsyncEnumerable<T> GetQueryResults(IQueryable<T> qry, [EnumeratorCancellation] CancellationToken cancelToken = default) {
			using (FeedIterator<T> FeedIterator = qry.ToFeedIterator()) {

				while (FeedIterator.HasMoreResults && !cancelToken.IsCancellationRequested) {
					foreach (var item in await FeedIterator.ReadNextAsync(cancelToken)) {
						yield return item;
					}
				}
			}
		}


		public async IAsyncEnumerable<M> ExecSQL<M>(string sql, [EnumeratorCancellation] CancellationToken cancelToken = default) {
			QueryDefinition def = new QueryDefinition(sql);


			using (FeedIterator<M> FeedIterator = this.Container.GetItemQueryIterator<M>(def)) {
				while (FeedIterator.HasMoreResults && !cancelToken.IsCancellationRequested) {
					foreach (var item in await FeedIterator.ReadNextAsync(cancelToken)) {
						yield return item;
					}
				}
			}
		}

		public async Task<string?> ReadSQL(string sql, Func<T, Task<bool>> onRead, string? partitionKey = null, string? continuationToken = null, int? maxItemCount = null, CancellationToken cancelToken = default) {
			QueryDefinition def = new QueryDefinition(sql);

			QueryRequestOptions options = new QueryRequestOptions();
			if (partitionKey != null) options.PartitionKey = new PartitionKey(partitionKey);
			if (maxItemCount != null) options.MaxItemCount = maxItemCount;

			string? token = Base64.Decode(continuationToken);
			bool run = true;
			int cnt = 0;
			using (FeedIterator<T> FeedIterator = this.Container.GetItemQueryIterator<T>(def, token, options)) {
				while (FeedIterator.HasMoreResults && !cancelToken.IsCancellationRequested && run) {
					var feed = await FeedIterator.ReadNextAsync(cancelToken);
					foreach (var item in feed) {
						cnt++;
						run = await onRead(item);
						if ((!run) || (cancelToken.IsCancellationRequested)) break;
					}
					token = Base64.Encode(feed.ContinuationToken);
					if ((maxItemCount != null) && (cnt >= maxItemCount)) run = false;
				}
			}
			return token;
		}


		public async Task<int> CountSQL(string where, System.Threading.CancellationToken cancelToken = default) {
			string sql = "select count(1) from c";
			if (!string.IsNullOrEmpty(where)) {
				sql += " where " + where;
			}

			try {
				using (FeedIterator<object> feed = this.Container.GetItemQueryIterator<object>(sql)) {
					while (feed.HasMoreResults) {
						FeedResponse<object> response = await feed.ReadNextAsync(cancelToken);
						foreach (var resp in response) {
							var jobj = (System.Text.Json.JsonElement)resp;
							return jobj.GetProperty("$1").GetInt32();
						}
						if (cancelToken.IsCancellationRequested) break;
					}
				}
			} catch (Exception err) {
			}
			return -1;
		}


		public async IAsyncEnumerable<JsonElement> RawSQL(string sql, [EnumeratorCancellation] System.Threading.CancellationToken cancelToken = default) {

			using (FeedIterator<object> feed = this.Container.GetItemQueryIterator<object>(sql)) {
				while (feed.HasMoreResults) {
					FeedResponse<object> response = await feed.ReadNextAsync(cancelToken);
					foreach (var resp in response) {
						if (resp is JsonElement el) {
							yield return el;
						}						
					}
					if (cancelToken.IsCancellationRequested) break;
				}
			}
		}


	}


}
