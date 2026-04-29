using Corner49.DB.Tools;
using Corner49.Infra.Tools;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Corner49.Infra.DB {



	public interface IDocumentRepo<T> where T : class {


		Action<DocumentDiagnostics>? OnDiagnostics { get; set; }

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

		Task<QueryResult<T>> Query(string? partitionKey, string sql, string? continuationToken = null, int? maxItemCount = null, Dictionary<string, object>? parameters = null, CancellationToken cancelToken = default);

		IAsyncEnumerable<M> ExecSQL<M>(string? partitionKey, string sql, int? maxItemCount = null, Dictionary<string, object>? parameters = null, CancellationToken cancelToken = default);

		Task<string?> ReadSQL(string? partitionKey, string sql, Func<T, Task<bool>> onRead, string? continuationToken = null, int? maxItemCount = null, Dictionary<string, object>? parameters = null, CancellationToken cancelToken = default);
		Task<int> CountSQL(string where, System.Threading.CancellationToken cancelToken = default);

		IAsyncEnumerable<JsonElement> RawSQL(string? partitionKey, string sql, Dictionary<string, object>? parameters = null, System.Threading.CancellationToken cancelToken = default);

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
		private readonly string[] _partitionKey;


		public DocumentRepo(CosmosClient client, string dbName, string containerName, params string[] paritionKey) {
			_client = client;

			_dbName = dbName;
			_containerName = containerName;
			_partitionKey = paritionKey;
			if (_partitionKey == null || _partitionKey.Length == 0) {
				_partitionKey = new string[] { "id" };
			}
		}


		private Database? _database;
		private Container? _container;

		protected Container Container {
			get {
				if (_database == null) _database = _client.GetDatabase(_dbName);
				if (_container == null) _container = _database.GetContainer(_containerName);
				return _container;
			}
		}

		Task IDocumentRepoInitializer.Init() {
			return this.Init(null, null);
		}

		public Action<DocumentDiagnostics>? OnDiagnostics { get; set; }

		public async Task Init(int? databaseThroughput = null, int? containerThroughput = null) {
			for (int retry = 0; retry <= 5; retry++) {
				try {
					var dbResp = await _client.CreateDatabaseIfNotExistsAsync(_dbName, databaseThroughput == null ? null : ThroughputProperties.CreateAutoscaleThroughput(databaseThroughput.Value));
					if (dbResp.Database == null) {
						throw new DocumentException($"Database '{_dbName}' not created");
					}

					if (_partitionKey.Length == 1) {
						var resp = await dbResp.Database.DefineContainer(_containerName, "/" + _partitionKey)
						.WithDefaultTimeToLive(-1)
						.CreateIfNotExistsAsync(containerThroughput == null ? null : ThroughputProperties.CreateAutoscaleThroughput(containerThroughput.Value));

					} else {
						ContainerProperties containerProperties = new ContainerProperties(
								id: _containerName,
								partitionKeyPaths: _partitionKey.Select(c => "/" + c).ToList()
						);
						containerProperties.DefaultTimeToLive = -1;

						var resp = await dbResp.Database.CreateContainerIfNotExistsAsync(containerProperties, containerThroughput == null ? null : ThroughputProperties.CreateAutoscaleThroughput(containerThroughput.Value));

					}

					return;
				} catch (CosmosException err) {
					if (err.StatusCode == System.Net.HttpStatusCode.TooManyRequests) {
						await Task.Delay(err.RetryAfter ?? TimeSpan.FromSeconds(10));
					} else {
						throw new DocumentException($"Init({_dbName},{_containerName}) failed", err);
					}
				} catch (Exception ex) {
					throw new DocumentException($"Init({_dbName},{_containerName}) failed", ex);
				}
			}
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



		public Task<T?> GetItem(string partitionId, string itemId) {
			return this.GetItem(new PartitionKey(partitionId), itemId);
		}
		public Task<T?> GetItem(string[] partitionId, string itemId) {
			PartitionKeyBuilder bld = new PartitionKeyBuilder();
			foreach (var pk in partitionId) {
				bld.Add(pk);
			}
			return this.GetItem(bld.Build(), itemId);
		}


		private async Task<T?> GetItem(PartitionKey pk, string itemId) {
			if (this.Container == null) throw new DocumentContainerNotFoundException(_containerName);

			Exception? lastErr = null;
			for (int retry = 0; retry <= 3; retry++) {
				try {
					var resp = await this.Container.ReadItemAsync<T>(itemId, pk);
					if (this.OnDiagnostics != null) {
						this.OnDiagnostics(new DocumentDiagnostics {
							Repo = this.GetType().Name,
							Method = nameof(GetItem),
							Parameters = new Dictionary<string, object?>() {
									{ "partitionKey", pk.ToString() },
									{ "itemId", itemId }
								},
							StatusCode = resp.StatusCode,
							StartTime = resp.Diagnostics.GetStartTimeUtc(),
							ElapsedTime = resp.Diagnostics.GetClientElapsedTime(),
							TotalRequestCharge = resp.Diagnostics.GetQueryMetrics().TotalRequestCharge
						});
					}

					if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
					return resp.Resource;
				} catch (CosmosException err) {
					lastErr = err;
					if (err.StatusCode == System.Net.HttpStatusCode.NotFound) {
						return null;
					} else if (err.StatusCode == System.Net.HttpStatusCode.TooManyRequests) {
						await Task.Delay(err.RetryAfter ?? TimeSpan.FromSeconds(5));
					} else if (err.StatusCode == System.Net.HttpStatusCode.RequestTimeout) {
						await Task.Delay(err.RetryAfter ?? TimeSpan.FromSeconds(5));
					} else {
						throw new DocumentException($"GetItem({pk},{itemId}) failed", err, err.StatusCode);
					}
				} catch (Exception ex) {
					throw new DocumentException($"GetItem({pk},{itemId}) failed", ex);
				}
			}
			throw new DocumentException($"GetItem({pk},{itemId}) failed", lastErr);
		}




		public IAsyncEnumerable<T> GetItems(string? paritionId) {
			if (this.Container == null) throw new DocumentContainerNotFoundException(_containerName);
			return this.GetQueryResults(this.CreateQuery(paritionId));
		}

		public Task<T> AddItem(string paritionId, T item) {
			return this.AddItem(new PartitionKey(paritionId), item);
		}
		public Task<T> AddItem(string[] partitionId, T item) {
			PartitionKeyBuilder bld = new PartitionKeyBuilder();
			foreach (var pk in partitionId) {
				bld.Add(pk);
			}
			return this.AddItem(bld.Build(), item);
		}

		private async Task<T> AddItem(PartitionKey pk, T item) {
			if (this.Container == null) throw new DocumentContainerNotFoundException(_containerName);

			Exception? lastErr = null;
			for (int retry = 0; retry <= 3; retry++) {
				try {
					var resp = await this.Container.CreateItemAsync(item, pk);
					if (this.OnDiagnostics != null) {
						this.OnDiagnostics(new DocumentDiagnostics {
							Repo = this.GetType().Name,
							Method = nameof(AddItem),
							Parameters = new Dictionary<string, object?>() {
									{ "partitionKey", pk.ToString() },
									{ "item", item }
								},
							StatusCode = resp.StatusCode,
							StartTime = resp.Diagnostics.GetStartTimeUtc(),
							ElapsedTime = resp.Diagnostics.GetClientElapsedTime(),
							TotalRequestCharge = resp.Diagnostics.GetQueryMetrics().TotalRequestCharge
						});
					}

					return resp.Resource;

				} catch (CosmosException err) {
					lastErr = err;
					if (err.StatusCode == System.Net.HttpStatusCode.TooManyRequests) {
						await Task.Delay(err.RetryAfter ?? TimeSpan.FromSeconds(5));
					} else if (err.StatusCode == System.Net.HttpStatusCode.RequestTimeout) {
						await Task.Delay(err.RetryAfter ?? TimeSpan.FromSeconds(5));
					} else {
						throw new DocumentException($"AddItem failed", err, err.StatusCode);
					}
				} catch (Exception ex) {
					throw new DocumentException($"AddItem failed", ex);
				}
			}
			throw new DocumentException($"AddItem failed", lastErr);
		}


		public Task<T> UpsertItem(string paritionId, T item, Action<HttpStatusCode>? status = null) {
			return this.UpsertItem(new PartitionKey(paritionId), item, status);
		}
		public Task<T> UpsertItem(string[] partitionId, T item, Action<HttpStatusCode>? status = null) {
			PartitionKeyBuilder bld = new PartitionKeyBuilder();
			foreach (var pk in partitionId) {
				bld.Add(pk);
			}
			return this.UpsertItem(bld.Build(), item, status);
		}
		private async Task<T> UpsertItem(PartitionKey pk, T item, Action<HttpStatusCode>? status = null) {
			if (this.Container == null) throw new DocumentContainerNotFoundException(_containerName);

			Exception? lastErr = null;
			for (int retry = 0; retry <= 3; retry++) {
				try {
					var resp = await this.Container.UpsertItemAsync(item, pk);
					if (this.OnDiagnostics != null) {
						this.OnDiagnostics(new DocumentDiagnostics {
							Repo = this.GetType().Name,
							Method = nameof(UpsertItem),
							Parameters = new Dictionary<string, object?>() {
									{ "partitionKey", pk.ToString() },
									{ "item", item }
								},
							StatusCode = resp.StatusCode,
							StartTime = resp.Diagnostics.GetStartTimeUtc(),
							ElapsedTime = resp.Diagnostics.GetClientElapsedTime(),
							TotalRequestCharge = resp.Diagnostics.GetQueryMetrics().TotalRequestCharge
						});
					}

					if (status != null) status.Invoke(resp.StatusCode);
					return resp.Resource;

				} catch (CosmosException err) {
					lastErr = err;
					if (err.StatusCode == System.Net.HttpStatusCode.TooManyRequests) {
						await Task.Delay(err.RetryAfter ?? TimeSpan.FromSeconds(5));
					} else if (err.StatusCode == System.Net.HttpStatusCode.RequestTimeout) {
						await Task.Delay(err.RetryAfter ?? TimeSpan.FromSeconds(5));
					} else {
						throw new DocumentException($"UpsertItem failed", err, err.StatusCode);
					}
				} catch (Exception err) {
					throw new DocumentException($"UpsertItem failed", err);
				}
			}
			throw new DocumentException($"UpsertItem failed", lastErr);
		}

		public Task<T> PatchItem(string paritionId, string itemId, IReadOnlyList<PatchOperation> patches) {
			return this.PatchItem(new PartitionKey(paritionId), itemId, patches);
		}
		public Task<T> PatchItem(string[] partitionId, string itemId, IReadOnlyList<PatchOperation> patches) {
			PartitionKeyBuilder bld = new PartitionKeyBuilder();
			foreach (var pk in partitionId) {
				bld.Add(pk);
			}
			return this.PatchItem(bld.Build(), itemId, patches);
		}
		private async Task<T> PatchItem(PartitionKey pk, string itemId, IReadOnlyList<PatchOperation> patches) {
			if (this.Container == null) throw new DocumentContainerNotFoundException(_containerName);

			Exception? lastErr = null;
			for (int retry = 0; retry <= 3; retry++) {
				try {
					var resp = await this.Container.PatchItemAsync<T>(itemId, pk, patches);
					if (this.OnDiagnostics != null) {
						this.OnDiagnostics(new DocumentDiagnostics {
							Repo = this.GetType().Name,
							Method = nameof(PatchItem),
							Parameters = new Dictionary<string, object?>() {
									{ "partitionKey", pk.ToString() },
									{ "itemId", itemId },
									{ "patches", patches },
								},
							StatusCode = resp.StatusCode,
							StartTime = resp.Diagnostics.GetStartTimeUtc(),
							ElapsedTime = resp.Diagnostics.GetClientElapsedTime(),
							TotalRequestCharge = resp.Diagnostics.GetQueryMetrics().TotalRequestCharge
						});
					}


					return resp.Resource;
				} catch (CosmosException err) {
					lastErr = err;
					if (err.StatusCode == System.Net.HttpStatusCode.TooManyRequests) {
						await Task.Delay(err.RetryAfter ?? TimeSpan.FromSeconds(5));
					} else if (err.StatusCode == System.Net.HttpStatusCode.RequestTimeout) {
						await Task.Delay(err.RetryAfter ?? TimeSpan.FromSeconds(5));
					} else {
						throw new DocumentException($"PatchItem({pk},{itemId}) failed", err, err.StatusCode);
					}
				} catch (Exception err) {
					throw new DocumentException($"PatchItem({pk},{itemId}) failed", err);
				}
			}
			throw new DocumentException($"PatchItem({pk},{itemId}) failed", lastErr);

		}

		public Task<bool> DeleteItem(string paritionId, string itemId) {
			return this.DeleteItem(new PartitionKey(paritionId), itemId);
		}
		public Task<bool> DeleteItem(string[] partitionId, string itemId) {
			PartitionKeyBuilder bld = new PartitionKeyBuilder();
			foreach (var pk in partitionId) {
				bld.Add(pk);
			}
			return this.DeleteItem(bld.Build(), itemId);
		}

		private async Task<bool> DeleteItem(PartitionKey pk, string itemId) {
			if (this.Container == null) throw new DocumentContainerNotFoundException(_containerName);


			Exception? lastErr = null;
			for (int retry = 0; retry <= 3; retry++) {
				try {
					var resp = await this.Container.DeleteItemAsync<T>(itemId, pk);
					if (this.OnDiagnostics != null) {
						this.OnDiagnostics(new DocumentDiagnostics {
							Repo = this.GetType().Name,
							Method = nameof(DeleteItem),
							Parameters = new Dictionary<string, object?>() {
									{ "partitionKey", pk.ToString() },
									{ "itemId", itemId }
								},
							StatusCode = resp.StatusCode,
							StartTime = resp.Diagnostics.GetStartTimeUtc(),
							ElapsedTime = resp.Diagnostics.GetClientElapsedTime(),
							TotalRequestCharge = resp.Diagnostics.GetQueryMetrics().TotalRequestCharge
						});
					}
					if (resp.StatusCode == System.Net.HttpStatusCode.NoContent) return true;
					if (resp.StatusCode == System.Net.HttpStatusCode.OK) return true;
					if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
					return false;
				} catch (CosmosException err) {
					lastErr = err;
					if (err.StatusCode == System.Net.HttpStatusCode.TooManyRequests) {
						await Task.Delay(err.RetryAfter ?? TimeSpan.FromSeconds(5));
					} else if (err.StatusCode == System.Net.HttpStatusCode.RequestTimeout) {
						await Task.Delay(err.RetryAfter ?? TimeSpan.FromSeconds(5));
					} else if (err.StatusCode == System.Net.HttpStatusCode.NotFound) {
						return false;
					} else {
						throw new DocumentException($"DeleteItem({pk},{itemId}) failed", err, err.StatusCode);
					}
				} catch (Exception err) {
					throw new DocumentException($"DeleteItem({pk},{itemId}) failed", err);
				}
			}
			throw new DocumentException($"DeleteItem({pk},{itemId}) failed", lastErr);


		}




		public IQueryable<T> CreateQuery(string? partitionKey = null, int? maxItemCount = null) {
			return CreateQuery(new PartitionKey(partitionKey), maxItemCount);
		}
		public IQueryable<T> CreateQuery(string[] partitionKey, int? maxItemCount = null) {
			PartitionKeyBuilder bld = new PartitionKeyBuilder();
			foreach (var pk in partitionKey) {
				bld.Add(pk);
			}
			return CreateQuery(bld.Build(), maxItemCount);
		}


		private IQueryable<T> CreateQuery(PartitionKey? partitionKey = null, int? maxItemCount = null) {
			if (this.Container == null) throw new DocumentContainerNotFoundException(_containerName);

			QueryRequestOptions queryOptions = new QueryRequestOptions();
			if (partitionKey != null) queryOptions.PartitionKey = partitionKey;
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

					if (this.OnDiagnostics != null) {
						this.OnDiagnostics(new DocumentDiagnostics {
							Repo = this.GetType().Name,
							Method = nameof(Read),
							Parameters = new Dictionary<string, object?>() {
									{ "partitionKey", partitionKey  },
									{ "filter", filter }
								},
							StatusCode = feed.StatusCode,
							StartTime = feed.Diagnostics.GetStartTimeUtc(),
							ElapsedTime = feed.Diagnostics.GetClientElapsedTime(),
							TotalRequestCharge = feed.Diagnostics.GetQueryMetrics().TotalRequestCharge
						});
					}


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

		public Task<QueryResult<T>> Query(string? partitionKey, string sql, string? continuationToken = null, int? maxItemCount = null, Dictionary<string, object>? parameters = null, CancellationToken cancelToken = default) {
			return this.Query(partitionKey != null ? new PartitionKey(partitionKey) : (PartitionKey?)null, sql, continuationToken, maxItemCount, parameters, cancelToken);
		}
		public Task<QueryResult<T>> Query(string[] partitionKey, string sql, string? continuationToken = null, int? maxItemCount = null, Dictionary<string, object>? parameters = null, CancellationToken cancelToken = default) {
			PartitionKeyBuilder bld = new PartitionKeyBuilder();
			foreach (var pk in partitionKey) {
				bld.Add(pk);
			}
			return this.Query(bld.Build(), sql, continuationToken, maxItemCount, parameters, cancelToken);
		}

		private async Task<QueryResult<T>> Query(PartitionKey? pk, string sql, string? continuationToken = null, int? maxItemCount = null, Dictionary<string, object>? parameters = null, CancellationToken cancelToken = default) {
			if (this.Container == null) throw new DocumentContainerNotFoundException(_containerName);

			QueryRequestOptions queryOptions = new QueryRequestOptions();
			if (pk != null) queryOptions.PartitionKey = pk;
			queryOptions.MaxItemCount = maxItemCount ?? -1;


			string? token = Base64.Decode(continuationToken);
			QueryDefinition def = new QueryDefinition(sql);
			if (parameters != null) {
				foreach (var kv in parameters) {
					def.WithParameter(kv.Key, kv.Value);
				}
			}


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

					if (this.OnDiagnostics != null) {
						this.OnDiagnostics(new DocumentDiagnostics {
							Repo = this.GetType().Name,
							Method = nameof(Query),
							Parameters = new Dictionary<string, object?>() {
									{ "partitionKey", pk?.ToString()  },
									{ "sql", sql },
									{ "parameters", parameters }
								},
							StatusCode = feed.StatusCode,
							StartTime = feed.Diagnostics.GetStartTimeUtc(),
							ElapsedTime = feed.Diagnostics.GetClientElapsedTime(),
							TotalRequestCharge = feed.Diagnostics.GetQueryMetrics().TotalRequestCharge
						});
					}

					foreach (var item in feed) {
						result.Data.Add(item);
					}
					result.ContinuationToken = Base64.Encode(feed.ContinuationToken);

					if ((maxItemCount != null) && (result.Data.Count >= maxItemCount)) break;
				}
			}

			return result;
		}


		public Task Read(string? partitionKey, Func<IQueryable<T>, IQueryable<T>> query, Func<T, int?, Task<bool>> onRead, int? maxItemCount = null, CancellationToken cancelToken = default) {
			var pk = partitionKey != null ? new PartitionKey(partitionKey) : (PartitionKey?)null;
			return this.Read(pk, query, onRead, maxItemCount, cancelToken);
		}
		public Task Read(string[] partitionKey, Func<IQueryable<T>, IQueryable<T>> query, Func<T, int?, Task<bool>> onRead, int? maxItemCount = null, CancellationToken cancelToken = default) {
			PartitionKeyBuilder bld = new PartitionKeyBuilder();
			foreach (var pk in partitionKey) {
				bld.Add(pk);
			}
			return this.Read(bld.Build(), query, onRead, maxItemCount, cancelToken);
		}

		private async Task Read(PartitionKey? partitionKey, Func<IQueryable<T>, IQueryable<T>> query, Func<T, int?, Task<bool>> onRead, int? maxItemCount = null, CancellationToken cancelToken = default) {
			if (this.Container == null) throw new DocumentContainerNotFoundException(_containerName);

			QueryRequestOptions queryOptions = new QueryRequestOptions();
			if (partitionKey != null) queryOptions.PartitionKey = partitionKey;
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




		public IAsyncEnumerable<M> ExecSQL<M>(string? partitionKey, string sql, int? maxItemCount = null, Dictionary<string, object>? parameters = null, [EnumeratorCancellation] CancellationToken cancelToken = default) {
			var pk = partitionKey != null ? new PartitionKey(partitionKey) : (PartitionKey?)null;
			return this.ExecSQL<M>(pk, sql, maxItemCount, parameters, cancelToken);
		}
		public IAsyncEnumerable<M> ExecSQL<M>(string[] partitionKey, string sql, int? maxItemCount = null, Dictionary<string, object>? parameters = null, [EnumeratorCancellation] CancellationToken cancelToken = default) {
			PartitionKeyBuilder bld = new PartitionKeyBuilder();
			foreach (var pk in partitionKey) {
				bld.Add(pk);
			}
			return this.ExecSQL<M>(bld.Build(), sql, maxItemCount, parameters, cancelToken);

		}

		private async IAsyncEnumerable<M> ExecSQL<M>(PartitionKey? partitionKey, string sql, int? maxItemCount = null, Dictionary<string, object>? parameters = null, [EnumeratorCancellation] CancellationToken cancelToken = default) {
			QueryDefinition def = new QueryDefinition(sql);
			if (parameters != null) {
				foreach (var kv in parameters) {
					def.WithParameter(kv.Key, kv.Value);
				}
			}


			QueryRequestOptions options = new QueryRequestOptions();
			if (partitionKey != null) options.PartitionKey = partitionKey;
			if (maxItemCount != null) options.MaxItemCount = maxItemCount;


			using (FeedIterator<M> feedIterator = this.Container.GetItemQueryIterator<M>(def, null, options)) {
				while (feedIterator.HasMoreResults && !cancelToken.IsCancellationRequested) {
					FeedResponse<M> feed = await feedIterator.ReadNextAsync(cancelToken);

					if (this.OnDiagnostics != null) {
						this.OnDiagnostics(new DocumentDiagnostics {
							Repo = this.GetType().Name,
							Method = nameof(ExecSQL),
							Parameters = new Dictionary<string, object?>() {
									{ "partitionKey", partitionKey?.ToString()  },
									{ "sql", sql },
									{  "maxItemCount", maxItemCount  },
									{ "parameters", parameters }
								},
							StatusCode = feed.StatusCode,
							StartTime = feed.Diagnostics.GetStartTimeUtc(),
							ElapsedTime = feed.Diagnostics.GetClientElapsedTime(),
							TotalRequestCharge = feed.Diagnostics.GetQueryMetrics().TotalRequestCharge
						});
					}



					while (feed.StatusCode == HttpStatusCode.TooManyRequests) {
						await Task.Delay(TimeSpan.FromSeconds(5));
						feed = await feedIterator.ReadNextAsync(cancelToken);

						if (this.OnDiagnostics != null) {
							this.OnDiagnostics(new DocumentDiagnostics {
								Repo = this.GetType().Name,
								Method = nameof(ExecSQL),
								Parameters = new Dictionary<string, object?>() {
									{ "partitionKey", partitionKey?.ToString()  },
									{ "sql", sql },
									{  "maxItemCount", maxItemCount  },
									{ "parameters", parameters }
								},
								StatusCode = feed.StatusCode,
								StartTime = feed.Diagnostics.GetStartTimeUtc(),
								ElapsedTime = feed.Diagnostics.GetClientElapsedTime(),
								TotalRequestCharge = feed.Diagnostics.GetQueryMetrics().TotalRequestCharge
							});
						}

					}

					foreach (var item in feed) {
						yield return item;
					}
				}
			}
		}

		public Task<string?> ReadSQL(string? partitionKey, string sql, Func<T, Task<bool>> onRead, string? continuationToken = null, int? maxItemCount = null, Dictionary<string, object>? parameters = null, CancellationToken cancelToken = default) {
			var pk = partitionKey != null ? new PartitionKey(partitionKey) : (PartitionKey?)null;
			return this.ReadSQL(pk, sql, onRead, continuationToken, maxItemCount, parameters, cancelToken);
		}
		public Task<string?> ReadSQL(string[] partitionKey, string sql, Func<T, Task<bool>> onRead, string? continuationToken = null, int? maxItemCount = null, Dictionary<string, object>? parameters = null, CancellationToken cancelToken = default) {
			PartitionKeyBuilder bld = new PartitionKeyBuilder();
			foreach (var pk in partitionKey) {
				bld.Add(pk);
			}
			return this.ReadSQL(bld.Build(), sql, onRead, continuationToken, maxItemCount, parameters, cancelToken);
		}

		private async Task<string?> ReadSQL(PartitionKey? partitionKey, string sql, Func<T, Task<bool>> onRead, string? continuationToken = null, int? maxItemCount = null, Dictionary<string, object>? parameters = null, CancellationToken cancelToken = default) {
			QueryDefinition def = new QueryDefinition(sql);
			if (parameters != null) {
				foreach (var kv in parameters) {
					def.WithParameter(kv.Key, kv.Value);
				}
			}

			QueryRequestOptions options = new QueryRequestOptions();
			if (partitionKey != null) options.PartitionKey = partitionKey;
			if (maxItemCount != null) options.MaxItemCount = maxItemCount;

			string? token = Base64.Decode(continuationToken);
			string? nextToken = null;
			bool run = true;
			int cnt = 0;
			using (FeedIterator<T> FeedIterator = this.Container.GetItemQueryIterator<T>(def, token, options)) {
				while (FeedIterator.HasMoreResults && !cancelToken.IsCancellationRequested && run) {
					var feed = await FeedIterator.ReadNextAsync(cancelToken);

					if (this.OnDiagnostics != null) {
						this.OnDiagnostics(new DocumentDiagnostics {
							Repo = this.GetType().Name,
							Method = nameof(ReadSQL),
							Parameters = new Dictionary<string, object?>() {
									{ "partitionKey", partitionKey?.ToString()  },
									{ "sql", sql },
									{ "continuationToken", continuationToken },
									{  "maxItemCount", maxItemCount  },
									{ "parameters", parameters }
								},
							StatusCode = feed.StatusCode,
							StartTime = feed.Diagnostics.GetStartTimeUtc(),
							ElapsedTime = feed.Diagnostics.GetClientElapsedTime(),
							TotalRequestCharge = feed.Diagnostics.GetQueryMetrics().TotalRequestCharge
						});
					}



					foreach (var item in feed) {
						cnt++;
						run = await onRead(item);
						if ((!run) || (cancelToken.IsCancellationRequested)) break;
					}
					nextToken = Base64.Encode(feed.ContinuationToken);
					if ((maxItemCount != null) && (cnt >= maxItemCount)) run = false;
				}
			}
			return nextToken;
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

						if (this.OnDiagnostics != null) {
							this.OnDiagnostics(new DocumentDiagnostics {
								Repo = this.GetType().Name,
								Method = nameof(CountSQL),
								Parameters = new Dictionary<string, object?>() {
									{ "where", where }
								},
								StatusCode = response.StatusCode,
								StartTime = response.Diagnostics.GetStartTimeUtc(),
								ElapsedTime = response.Diagnostics.GetClientElapsedTime(),
								TotalRequestCharge = response.Diagnostics.GetQueryMetrics().TotalRequestCharge
							});
						}



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


		public IAsyncEnumerable<JsonElement> RawSQL(string? partitionKey, string sql, Dictionary<string, object>? parameters = null, [EnumeratorCancellation] System.Threading.CancellationToken cancelToken = default) {
			var pk = partitionKey != null ? new PartitionKey(partitionKey) : (PartitionKey?)null;
			return RawSQL(pk, sql, parameters, cancelToken);
		}
		public IAsyncEnumerable<JsonElement> RawSQL(string[]? partitionKey, string sql, Dictionary<string, object>? parameters = null, [EnumeratorCancellation] System.Threading.CancellationToken cancelToken = default) {
			PartitionKeyBuilder bld = new PartitionKeyBuilder();
			foreach (var pk in partitionKey) {
				bld.Add(pk);
			}
			return RawSQL(bld.Build(), sql, parameters, cancelToken);
		}


		private async IAsyncEnumerable<JsonElement> RawSQL(PartitionKey? pk, string sql, Dictionary<string, object>? parameters = null, [EnumeratorCancellation] System.Threading.CancellationToken cancelToken = default) {
			QueryDefinition def = new QueryDefinition(sql);
			if (parameters != null) {
				foreach (var kv in parameters) {
					def.WithParameter(kv.Key, kv.Value);
				}
			}

			QueryRequestOptions options = new QueryRequestOptions();
			if (pk != null) options.PartitionKey = pk;

			using (FeedIterator<object> feed = this.Container.GetItemQueryIterator<object>(def, null, options)) {
				while (feed.HasMoreResults) {
					FeedResponse<object> response = await feed.ReadNextAsync(cancelToken);


					if (this.OnDiagnostics != null) {
						this.OnDiagnostics(new DocumentDiagnostics {
							Repo = this.GetType().Name,
							Method = nameof(RawSQL),
							Parameters = new Dictionary<string, object?>() {
									{ "partitionKey", pk?.ToString()  },
									{ "sql", sql },
									{ "parameters", parameters }
								},
							StatusCode = response.StatusCode,
							StartTime = response.Diagnostics.GetStartTimeUtc(),
							ElapsedTime = response.Diagnostics.GetClientElapsedTime(),
							TotalRequestCharge = response.Diagnostics.GetQueryMetrics().TotalRequestCharge
						});
					}


					foreach (var resp in response) {
						if (resp is JsonElement el) {
							yield return el;
						}
					}
					if (cancelToken.IsCancellationRequested) break;
				}
			}
		}

		public async Task BulkInsert(IAsyncEnumerable<T> items, Func<T, string> getPartitionKey, CancellationToken cancellationToken = default) {
			var container = this.Container;

			List<Task> tasks = new List<Task>();
			await foreach (var itm in items) {
				if (cancellationToken.IsCancellationRequested) break;

				try {
					tasks.Add(container.CreateItemAsync(itm, new PartitionKey(getPartitionKey(itm)), null, cancellationToken)
							.ContinueWith(itemResponse => {
								if (!itemResponse.IsCompletedSuccessfully) {
									AggregateException innerExceptions = itemResponse.Exception.Flatten();
									if (innerExceptions.InnerExceptions.FirstOrDefault(innerEx => innerEx is CosmosException) is CosmosException cosmosException) {
										Console.WriteLine($"Received {cosmosException.StatusCode} ({cosmosException.Message}).");
									} else {
										Console.WriteLine($"Exception {innerExceptions.InnerExceptions.FirstOrDefault()}.");
									}
								}
							}));
				} catch (Exception ex) {
				}
			}

			await Task.WhenAll(tasks);
		}

		public async Task BulkDelete(IAsyncEnumerable<T> items, Func<T, string> getId, Func<T, string> getPartitionKey, CancellationToken cancellationToken = default) {
			var container = this.Container;

			List<Task> tasks = new List<Task>();
			await foreach (var itm in items) {
				if (cancellationToken.IsCancellationRequested) break;

				try {
					tasks.Add(container.DeleteItemAsync<T>(getId(itm), new PartitionKey(getPartitionKey(itm)), null, cancellationToken)
							.ContinueWith(itemResponse => {
								if (!itemResponse.IsCompletedSuccessfully) {
									AggregateException innerExceptions = itemResponse.Exception.Flatten();
									if (innerExceptions.InnerExceptions.FirstOrDefault(innerEx => innerEx is CosmosException) is CosmosException cosmosException) {
										Console.WriteLine($"Received {cosmosException.StatusCode} ({cosmosException.Message}).");
									} else {
										Console.WriteLine($"Exception {innerExceptions.InnerExceptions.FirstOrDefault()}.");
									}
								}
							}));
				} catch (Exception err) {

				}
			}

			await Task.WhenAll(tasks);
		}

		public async Task BulkUpdate(IAsyncEnumerable<T> items, Func<T, string> getId, Func<T, string> getPartitionKey, Func<T, T>? update = null, CancellationToken cancellationToken = default) {
			var container = this.Container;

			List<Task> tasks = new List<Task>();
			await foreach (var itm in items) {
				if (cancellationToken.IsCancellationRequested) break;

				try {
					T item = update == null ? itm : update(itm);

					tasks.Add(container.ReplaceItemAsync(item, getId(itm), new PartitionKey(getPartitionKey(itm)), null, cancellationToken)
							.ContinueWith(itemResponse => {
								if (!itemResponse.IsCompletedSuccessfully) {
									AggregateException innerExceptions = itemResponse.Exception.Flatten();
									if (innerExceptions.InnerExceptions.FirstOrDefault(innerEx => innerEx is CosmosException) is CosmosException cosmosException) {
										Console.WriteLine($"Received {cosmosException.StatusCode} ({cosmosException.Message}).");
									} else {
										Console.WriteLine($"Exception {innerExceptions.InnerExceptions.FirstOrDefault()}.");
									}
								}
							}));
				} catch (Exception err) {
				}
			}

			await Task.WhenAll(tasks);
		}


	}


}
