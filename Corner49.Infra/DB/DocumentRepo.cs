using Corner49.DB.Tools;
using Corner49.Infra.Tools;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Corner49.Infra.DB {

	/// <summary>
	/// Repository interface for Azure Cosmos DB NoSQL API operations.
	/// Provides CRUD operations, querying, and change feed capabilities.
	/// </summary>
	/// <typeparam name="T">The document type to store and retrieve from Cosmos DB.</typeparam>
	/// <remarks>
	/// PERFORMANCE BEST PRACTICES:
	/// - Always specify partition keys to avoid expensive cross-partition queries
	/// - Use async enumerable methods for large result sets to minimize memory usage
	/// - Leverage LINQ queries with CreateQuery for type-safe, optimized queries
	/// - Monitor RU consumption via OnDiagnostics callback
	/// - Use UpsertItem instead of separate read/write operations when appropriate
	/// - Implement continuation tokens for pagination in production scenarios
	/// </remarks>
	public interface IDocumentRepo<T> where T : class {

		/// <summary>
		/// Optional callback to capture diagnostic information for all operations.
		/// Use this to monitor RU consumption, latency, and performance metrics.
		/// </summary>
		Action<DocumentDiagnostics>? OnDiagnostics { get; set; }

		/// <summary>
		/// Retrieves a single document by its partition key and item ID.
		/// </summary>
		/// <param name="paritionId">The partition key value (single partition key).</param>
		/// <param name="itemId">The unique item ID within the partition.</param>
		/// <returns>The document if found, otherwise null.</returns>
		/// <remarks>
		/// This is the most efficient way to retrieve a document (point read).
		/// Typically consumes 1 RU for documents up to 1KB.
		/// </remarks>
		Task<T?> GetItem(string paritionId, string itemId);

		/// <summary>
		/// Retrieves a single document by its hierarchical partition key and item ID.
		/// </summary>
		/// <param name="paritionId">The hierarchical partition key values.</param>
		/// <param name="itemId">The unique item ID within the partition.</param>
		/// <returns>The document if found, otherwise null.</returns>
		/// <remarks>
		/// Use this overload for containers with hierarchical partition keys.
		/// Order of partition key values must match the container definition.
		/// </remarks>
		Task<T?> GetItem(string[] paritionId, string itemId);

		/// <summary>
		/// Retrieves all documents within a partition or the entire container.
		/// </summary>
		/// <param name="paritionId">The partition key value, or null for cross-partition query.</param>
		/// <returns>Async enumerable stream of documents.</returns>
		/// <remarks>
		/// WARNING: Cross-partition queries (null partitionKey) can be expensive.
		/// Results are streamed to minimize memory usage. RU cost depends on result set size.
		/// </remarks>
		IAsyncEnumerable<T> GetItems(string? paritionId);

		/// <summary>
		/// Retrieves all documents within a hierarchical partition.
		/// </summary>
		/// <param name="paritionId">The hierarchical partition key values.</param>
		/// <returns>Async enumerable stream of documents.</returns>
		IAsyncEnumerable<T> GetItems(string[] paritionId);

		/// <summary>
		/// Creates a new document in the container.
		/// </summary>
		/// <param name="paritionId">The partition key value (single partition key).</param>
		/// <param name="item">The document to create.</param>
		/// <returns>The created document with server-generated properties.</returns>
		/// <remarks>
		/// Fails if a document with the same ID already exists.
		/// Use UpsertItem if you want to create or replace.
		/// </remarks>
		Task<T> AddItem(string paritionId, T item);

		/// <summary>
		/// Creates a new document in a container with hierarchical partition keys.
		/// </summary>
		/// <param name="paritionId">The hierarchical partition key values.</param>
		/// <param name="item">The document to create.</param>
		/// <returns>The created document with server-generated properties.</returns>
		Task<T> AddItem(string[] paritionId, T item);

		/// <summary>
		/// Creates or replaces a document (insert or update).
		/// </summary>
		/// <param name="paritionId">The partition key value (single partition key).</param>
		/// <param name="item">The document to upsert.</param>
		/// <param name="status">Optional callback to receive the HTTP status code (Created or OK).</param>
		/// <returns>The upserted document.</returns>
		/// <remarks>
		/// More efficient than separate read + write operations.
		/// Status code indicates whether item was created (201) or replaced (200).
		/// </remarks>
		Task<T> UpsertItem(string paritionId, T item, Action<HttpStatusCode>? status = null);

		/// <summary>
		/// Creates or replaces a document in a container with hierarchical partition keys.
		/// </summary>
		/// <param name="paritionId">The hierarchical partition key values.</param>
		/// <param name="item">The document to upsert.</param>
		/// <param name="status">Optional callback to receive the HTTP status code.</param>
		/// <returns>The upserted document.</returns>
		Task<T> UpsertItem(string[] paritionId, T item, Action<HttpStatusCode>? status = null);

		/// <summary>
		/// Partially updates a document using patch operations.
		/// </summary>
		/// <param name="partitionId">The partition key value (single partition key).</param>
		/// <param name="itemId">The unique item ID to patch.</param>
		/// <param name="patches">List of patch operations to apply.</param>
		/// <returns>The updated document.</returns>
		/// <remarks>
		/// More efficient than replace for updating specific fields.
		/// Reduces network payload and RU consumption compared to full document updates.
		/// </remarks>
		Task<T> PatchItem(string partitionId, string itemId, IReadOnlyList<PatchOperation> patches);

		/// <summary>
		/// Partially updates a document in a container with hierarchical partition keys.
		/// </summary>
		/// <param name="partitionId">The hierarchical partition key values.</param>
		/// <param name="itemId">The unique item ID to patch.</param>
		/// <param name="patches">List of patch operations to apply.</param>
		/// <returns>The updated document.</returns>
		Task<T> PatchItem(string[] partitionId, string itemId, IReadOnlyList<PatchOperation> patches);

		/// <summary>
		/// Deletes a document from the container.
		/// </summary>
		/// <param name="paritionId">The partition key value (single partition key).</param>
		/// <param name="itemId">The unique item ID to delete.</param>
		/// <returns>True if deleted, false if not found.</returns>
		Task<bool> DeleteItem(string paritionId, string itemId);

		/// <summary>
		/// Deletes a document from a container with hierarchical partition keys.
		/// </summary>
		/// <param name="paritionId">The hierarchical partition key values.</param>
		/// <param name="itemId">The unique item ID to delete.</param>
		/// <returns>True if deleted, false if not found.</returns>
		Task<bool> DeleteItem(string[] paritionId, string itemId);

		/// <summary>
		/// Creates a LINQ queryable for building type-safe queries.
		/// </summary>
		/// <param name="paritionKey">Optional partition key to scope the query, or null for cross-partition.</param>
		/// <param name="maxItemCount">Maximum items per page. Controls memory usage and pagination.</param>
		/// <returns>IQueryable for building LINQ queries.</returns>
		/// <remarks>
		/// Use this for type-safe, optimized queries with LINQ syntax.
		/// WARNING: Null partitionKey enables cross-partition queries which can be expensive.
		/// </remarks>
		IQueryable<T> CreateQuery(string? paritionKey = null, int? maxItemCount = null);

		/// <summary>
		/// Creates a LINQ queryable scoped to a hierarchical partition.
		/// </summary>
		/// <param name="paritionKey">The hierarchical partition key values.</param>
		/// <param name="maxItemCount">Maximum items per page.</param>
		/// <returns>IQueryable for building LINQ queries.</returns>
		IQueryable<T> CreateQuery(string[] paritionKey, int? maxItemCount = null);

		/// <summary>
		/// Executes a query and streams results through a callback function.
		/// </summary>
		/// <param name="partitionKey">Optional partition key, or null for cross-partition query.</param>
		/// <param name="query">Function to build the LINQ query.</param>
		/// <param name="onRead">Callback invoked for each document. Return false to stop iteration.</param>
		/// <param name="maxItemCount">Maximum items per page.</param>
		/// <param name="cancelToken">Cancellation token.</param>
		/// <remarks>
		/// Efficient for processing large result sets without loading all into memory.
		/// The onRead callback receives the item and optional total count.
		/// </remarks>
		Task Read(string? partitionKey, Func<IQueryable<T>, IQueryable<T>> query, Func<T, int?, Task<bool>> onRead, int? maxItemCount = null, CancellationToken cancelToken = default);

		/// <summary>
		/// Executes a query with hierarchical partition key and streams results.
		/// </summary>
		/// <param name="partitionKey">The hierarchical partition key values.</param>
		/// <param name="query">Function to build the LINQ query.</param>
		/// <param name="onRead">Callback invoked for each document.</param>
		/// <param name="maxItemCount">Maximum items per page.</param>
		/// <param name="cancelToken">Cancellation token.</param>
		Task Read(string[] partitionKey, Func<IQueryable<T>, IQueryable<T>> query, Func<T, int?, Task<bool>> onRead, int? maxItemCount = null, CancellationToken cancelToken = default);

		/// <summary>
		/// Executes a queryable and returns results as an async enumerable.
		/// </summary>
		/// <param name="qry">The IQueryable to execute.</param>
		/// <param name="cancelToken">Cancellation token.</param>
		/// <returns>Async enumerable stream of documents.</returns>
		/// <remarks>
		/// Use after building query with CreateQuery() for memory-efficient result streaming.
		/// </remarks>
		IAsyncEnumerable<T> GetQueryResults(IQueryable<T> qry, CancellationToken cancelToken = default);

		/// <summary>
		/// Executes a query using a DocumentFilter with pagination support.
		/// </summary>
		/// <param name="partitionKey">Optional partition key, or null for cross-partition query.</param>
		/// <param name="filter">Filter object containing query logic and pagination tokens.</param>
		/// <param name="cancelToken">Cancellation token.</param>
		/// <returns>Query result with data and continuation token for next page.</returns>
		Task<QueryResult<T>> Filter(string? partitionKey, DocumentFilter<T> filter, CancellationToken cancelToken = default);

		/// <summary>
		/// Executes a LINQ query with pagination support.
		/// </summary>
		/// <param name="partitionKey">Optional partition key, or null for cross-partition query.</param>
		/// <param name="query">Function to build the LINQ query.</param>
		/// <param name="continuationToken">Token from previous query for pagination.</param>
		/// <param name="maxItemCount">Maximum items to return.</param>
		/// <param name="cancelToken">Cancellation token.</param>
		/// <returns>Query result with data, total count, and continuation token.</returns>
		/// <remarks>
		/// Use continuationToken for efficient pagination.
		/// TotalCount is only calculated on first page (when continuationToken is null).
		/// </remarks>
		Task<QueryResult<T>> Query(string? partitionKey, Func<IQueryable<T>, IQueryable<T>> query, string? continuationToken = null, int? maxItemCount = null, CancellationToken cancelToken = default);

		/// <summary>
		/// Executes a LINQ query with hierarchical partition key and pagination.
		/// </summary>
		/// <param name="partitionKey">The hierarchical partition key values.</param>
		/// <param name="query">Function to build the LINQ query.</param>
		/// <param name="continuationToken">Token from previous query for pagination.</param>
		/// <param name="maxItemCount">Maximum items to return.</param>
		/// <param name="cancelToken">Cancellation token.</param>
		/// <returns>Query result with data and continuation token.</returns>
		Task<QueryResult<T>> Query(string[] partitionKey, Func<IQueryable<T>, IQueryable<T>> query, string? continuationToken = null, int? maxItemCount = null, CancellationToken cancelToken = default);

		/// <summary>
		/// Executes a SQL query with pagination support.
		/// </summary>
		/// <param name="partitionKey">Optional partition key, or null for cross-partition query.</param>
		/// <param name="sql">SQL query string. Use @paramName for parameterized queries.</param>
		/// <param name="continuationToken">Token from previous query for pagination.</param>
		/// <param name="maxItemCount">Maximum items to return.</param>
		/// <param name="parameters">Query parameters (key = @paramName, value = param value).</param>
		/// <param name="cancelToken">Cancellation token.</param>
		/// <returns>Query result with data and continuation token.</returns>
		/// <remarks>
		/// Always use parameterized queries to prevent SQL injection.
		/// Example: sql="SELECT * FROM c WHERE c.status = @status", parameters={"@status", "active"}
		/// </remarks>
		Task<QueryResult<T>> Query(string? partitionKey, string sql, string? continuationToken = null, int? maxItemCount = null, Dictionary<string, object>? parameters = null, CancellationToken cancelToken = default);

		/// <summary>
		/// Executes a SQL query with hierarchical partition key and pagination.
		/// </summary>
		/// <param name="partitionKey">The hierarchical partition key values.</param>
		/// <param name="sql">SQL query string with parameters.</param>
		/// <param name="continuationToken">Token from previous query for pagination.</param>
		/// <param name="maxItemCount">Maximum items to return.</param>
		/// <param name="parameters">Query parameters.</param>
		/// <param name="cancelToken">Cancellation token.</param>
		/// <returns>Query result with data and continuation token.</returns>
		Task<QueryResult<T>> Query(string[] partitionKey, string sql, string? continuationToken = null, int? maxItemCount = null, Dictionary<string, object>? parameters = null, CancellationToken cancelToken = default);

		/// <summary>
		/// Executes a SQL query and returns results as an async enumerable with custom projection.
		/// </summary>
		/// <typeparam name="M">The result type (can be different from T for projections).</typeparam>
		/// <param name="partitionKey">Optional partition key, or null for cross-partition query.</param>
		/// <param name="sql">SQL query string with parameters.</param>
		/// <param name="maxItemCount">Maximum items per page.</param>
		/// <param name="parameters">Query parameters.</param>
		/// <param name="cancelToken">Cancellation token.</param>
		/// <returns>Async enumerable stream of results.</returns>
		/// <remarks>
		/// Use for memory-efficient processing of large result sets.
		/// Supports custom projections (e.g., SELECT c.name, c.status FROM c).
		/// </remarks>
		IAsyncEnumerable<M> ExecSQL<M>(string? partitionKey, string sql, int? maxItemCount = null, Dictionary<string, object>? parameters = null, CancellationToken cancelToken = default);

		/// <summary>
		/// Executes a SQL query with hierarchical partition key and returns results as async enumerable.
		/// </summary>
		/// <typeparam name="M">The result type.</typeparam>
		/// <param name="partitionKey">The hierarchical partition key values.</param>
		/// <param name="sql">SQL query string with parameters.</param>
		/// <param name="maxItemCount">Maximum items per page.</param>
		/// <param name="parameters">Query parameters.</param>
		/// <param name="cancelToken">Cancellation token.</param>
		/// <returns>Async enumerable stream of results.</returns>
		IAsyncEnumerable<M> ExecSQL<M>(string[] partitionKey, string sql, int? maxItemCount = null, Dictionary<string, object>? parameters = null, CancellationToken cancelToken = default);

		/// <summary>
		/// Executes a SQL query and streams results through a callback function.
		/// </summary>
		/// <param name="partitionKey">Optional partition key, or null for cross-partition query.</param>
		/// <param name="sql">SQL query string with parameters.</param>
		/// <param name="onRead">Callback invoked for each document. Return false to stop iteration.</param>
		/// <param name="continuationToken">Token from previous query for pagination.</param>
		/// <param name="maxItemCount">Maximum items per page.</param>
		/// <param name="parameters">Query parameters.</param>
		/// <param name="cancelToken">Cancellation token.</param>
		/// <returns>Continuation token for next page, or null if complete.</returns>
		/// <remarks>
		/// Efficient for processing results page by page.
		/// Return continuation token to caller for pagination support.
		/// </remarks>
		Task<string?> ReadSQL(string? partitionKey, string sql, Func<T, Task<bool>> onRead, string? continuationToken = null, int? maxItemCount = null, Dictionary<string, object>? parameters = null, CancellationToken cancelToken = default);

		/// <summary>
		/// Executes a SQL query with hierarchical partition key and streams results.
		/// </summary>
		/// <param name="partitionKey">The hierarchical partition key values.</param>
		/// <param name="sql">SQL query string with parameters.</param>
		/// <param name="onRead">Callback invoked for each document.</param>
		/// <param name="continuationToken">Token from previous query for pagination.</param>
		/// <param name="maxItemCount">Maximum items per page.</param>
		/// <param name="parameters">Query parameters.</param>
		/// <param name="cancelToken">Cancellation token.</param>
		/// <returns>Continuation token for next page.</returns>
		Task<string?> ReadSQL(string[] partitionKey, string sql, Func<T, Task<bool>> onRead, string? continuationToken = null, int? maxItemCount = null, Dictionary<string, object>? parameters = null, CancellationToken cancelToken = default);

		/// <summary>
		/// Counts documents matching a WHERE clause condition.
		/// </summary>
		/// <param name="where">WHERE clause condition (without the WHERE keyword).</param>
		/// <param name="cancelToken">Cancellation token.</param>
		/// <returns>Number of matching documents, or -1 on error.</returns>
		/// <remarks>
		/// WARNING: This performs a cross-partition query and can be expensive.
		/// Consider using Query with Take for pagination instead of counting all results.
		/// </remarks>
		Task<int> CountSQL(string where, System.Threading.CancellationToken cancelToken = default);

		/// <summary>
		/// Executes a SQL query and returns raw JSON elements.
		/// </summary>
		/// <param name="partitionKey">Optional partition key, or null for cross-partition query.</param>
		/// <param name="sql">SQL query string with parameters.</param>
		/// <param name="parameters">Query parameters.</param>
		/// <param name="cancelToken">Cancellation token.</param>
		/// <returns>Async enumerable stream of JsonElement objects.</returns>
		/// <remarks>
		/// Use for dynamic queries where result schema is not known at compile time.
		/// Returns raw JSON for maximum flexibility.
		/// </remarks>
		IAsyncEnumerable<JsonElement> RawSQL(string? partitionKey, string sql, Dictionary<string, object>? parameters = null, System.Threading.CancellationToken cancelToken = default);

		/// <summary>
		/// Creates a change feed processor to monitor container changes in real-time.
		/// </summary>
		/// <param name="changeHandler">Callback invoked when changes are detected.</param>
		/// <param name="leaseName">Name of the lease container (created if doesn't exist).</param>
		/// <param name="processorName">Unique name for this processor instance.</param>
		/// <param name="instanceName">Instance identifier for distributed processing.</param>
		/// <param name="onRelease">Optional callback when lease is released.</param>
		/// <param name="onError">Optional error handler callback.</param>
		/// <returns>ChangeFeedProcessor instance. Call StartAsync() to begin monitoring.</returns>
		/// <remarks>
		/// Change feed enables event-driven architectures and data synchronization.
		/// Multiple instances can process changes in parallel for scalability.
		/// </remarks>
		Task<ChangeFeedProcessor> GetChangeFeedProcessor(Container.ChangesHandler<T> changeHandler, string leaseName = "changeLeases", string? processorName = null, string? instanceName = null,
						Func<Task>? onRelease = null,
						Func<Exception, Task>? onError = null);

		/// <summary>
		/// Creates a change feed processor that provides full change feed context.
		/// </summary>
		/// <param name="changeHandler">Callback with full ChangeFeedProcessorContext.</param>
		/// <param name="leaseName">Name of the lease container.</param>
		/// <param name="processorName">Unique name for this processor instance.</param>
		/// <param name="instanceName">Instance identifier for distributed processing.</param>
		/// <param name="onRelease">Optional callback when lease is released.</param>
		/// <param name="onError">Optional error handler callback.</param>
		/// <returns>ChangeFeedProcessor instance.</returns>
		/// <remarks>
		/// Use this overload when you need access to change feed context and metadata.
		/// </remarks>
		Task<ChangeFeedProcessor> GetAllChangesFeedProcessor(Container.ChangeFeedHandler<T> changeHandler, string leaseName = "changeLeases", string? processorName = null, string? instanceName = null,
				Func<Task>? onRelease = null,
				Func<Exception, Task>? onError = null);
	}

	/// <summary>
	/// Interface for initializing document repositories.
	/// Used internally to create databases and containers.
	/// </summary>
	public interface IDocumentRepoInitializer {

		/// <summary>
		/// Initializes the database and container if they don't exist.
		/// </summary>
		/// <returns>Async task.</returns>
		Task Init();
	}

	/// <summary>
	/// Implementation of IDocumentRepo for Azure Cosmos DB NoSQL API operations.
	/// See <see cref="IDocumentRepo{T}"/> for detailed method documentation.
	/// </summary>
	/// <typeparam name="T">The document type.</typeparam>
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
	public class DocumentRepo<T> : IDocumentRepoInitializer, IDocumentRepo<T> where T : class {

		//		private readonly IConfiguration _config;

		private readonly CosmosClient _client;

		private readonly string _dbName;
		private readonly string _containerName;
		private readonly string[] _partitionKey;

		/// <summary>
		/// Initializes a new instance of the DocumentRepo class.
		/// </summary>
		/// <param name="client">The CosmosClient instance.</param>
		/// <param name="dbName">Database name.</param>
		/// <param name="containerName">Container name.</param>
		/// <param name="paritionKey">Partition key path(s).</param>
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

		/// <summary>
		/// Gets the Cosmos DB container instance.
		/// </summary>
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

		/// <inheritdoc />
		public Action<DocumentDiagnostics>? OnDiagnostics { get; set; }

		/// <summary>
		/// Initializes the database and container with optional throughput configuration.
		/// </summary>
		/// <param name="databaseThroughput">Optional autoscale throughput for database.</param>
		/// <param name="containerThroughput">Optional autoscale throughput for container.</param>
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

		/// <inheritdoc />
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

		/// <inheritdoc />
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

		/// <inheritdoc />
		public Task<T?> GetItem(string partitionId, string itemId) {
			return this.GetItem(new PartitionKey(partitionId), itemId);
		}
		/// <inheritdoc />
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

		/// <inheritdoc />
		public IAsyncEnumerable<T> GetItems(string? paritionId) {
			if (this.Container == null) throw new DocumentContainerNotFoundException(_containerName);
			return this.GetQueryResults(this.CreateQuery(paritionId));
		}

		/// <inheritdoc />
		public IAsyncEnumerable<T> GetItems(string[] paritionId) {
			if (this.Container == null) throw new DocumentContainerNotFoundException(_containerName);
			return this.GetQueryResults(this.CreateQuery(paritionId));
		}

		/// <inheritdoc />
		public Task<T> AddItem(string paritionId, T item) {
			return this.AddItem(new PartitionKey(paritionId), item);
		}
		/// <inheritdoc />
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

		/// <inheritdoc />
		public Task<T> UpsertItem(string paritionId, T item, Action<HttpStatusCode>? status = null) {
			return this.UpsertItem(new PartitionKey(paritionId), item, status);
		}
		/// <inheritdoc />
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

		/// <inheritdoc />
		public Task<T> PatchItem(string paritionId, string itemId, IReadOnlyList<PatchOperation> patches) {
			return this.PatchItem(new PartitionKey(paritionId), itemId, patches);
		}
		/// <inheritdoc />
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

		/// <inheritdoc />
		public Task<bool> DeleteItem(string paritionId, string itemId) {
			return this.DeleteItem(new PartitionKey(paritionId), itemId);
		}
		/// <inheritdoc />
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

		/// <inheritdoc />
		public IQueryable<T> CreateQuery(string? partitionKey = null, int? maxItemCount = null) {
			return CreateQuery(new PartitionKey(partitionKey), maxItemCount);
		}
		/// <inheritdoc />
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

		/// <summary>
		/// Reads documents using a filter with callback processing.
		/// </summary>
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

		/// <inheritdoc />
		public Task<QueryResult<T>> Filter(string? partitionKey, DocumentFilter<T> filter, CancellationToken cancelToken = default) {
			return this.Query(partitionKey, (qry) => filter.Build(qry), filter.ContinuationToken, filter.Take, cancelToken);
		}

		/// <inheritdoc />
		public Task<QueryResult<T>> Query(string? partitionKey, Func<IQueryable<T>, IQueryable<T>> query, string? continuationToken = null, int? maxItemCount = null, CancellationToken cancelToken = default) {
			var pk = partitionKey != null ? new PartitionKey(partitionKey) : (PartitionKey?)null;
			return this.Query(pk, query, continuationToken, maxItemCount, cancelToken);
		}
		/// <inheritdoc />
		public Task<QueryResult<T>> Query(string[] partitionKey, Func<IQueryable<T>, IQueryable<T>> query, string? continuationToken = null, int? maxItemCount = null, CancellationToken cancelToken = default) {
			PartitionKeyBuilder bld = new PartitionKeyBuilder();
			foreach (var pk in partitionKey) {
				bld.Add(pk);
			}

			return Query(bld.Build(), query, continuationToken, maxItemCount, cancelToken);
		}


		private async Task<QueryResult<T>> Query(PartitionKey? partitionKey, Func<IQueryable<T>, IQueryable<T>> query, string? continuationToken = null, int? maxItemCount = null, CancellationToken cancelToken = default) {
			if (this.Container == null) throw new DocumentContainerNotFoundException(_containerName);

			QueryRequestOptions queryOptions = new QueryRequestOptions();
			if (partitionKey != null) queryOptions.PartitionKey = partitionKey;
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

		/// <inheritdoc />
		public Task<QueryResult<T>> Query(string? partitionKey, string sql, string? continuationToken = null, int? maxItemCount = null, Dictionary<string, object>? parameters = null, CancellationToken cancelToken = default) {
			return this.Query(partitionKey != null ? new PartitionKey(partitionKey) : (PartitionKey?)null, sql, continuationToken, maxItemCount, parameters, cancelToken);
		}
		/// <inheritdoc />
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

		/// <inheritdoc />
		public Task Read(string? partitionKey, Func<IQueryable<T>, IQueryable<T>> query, Func<T, int?, Task<bool>> onRead, int? maxItemCount = null, CancellationToken cancelToken = default) {
			var pk = partitionKey != null ? new PartitionKey(partitionKey) : (PartitionKey?)null;
			return this.Read(pk, query, onRead, maxItemCount, cancelToken);
		}
		/// <inheritdoc />
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

		/// <inheritdoc />
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
		/// <inheritdoc />
		public IAsyncEnumerable<M> ExecSQL<M>(string[] partitionKey, string sql, int? maxItemCount = null, Dictionary<string, object>? parameters = null, CancellationToken cancelToken = default) {
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

		/// <inheritdoc />
		public Task<string?> ReadSQL(string? partitionKey, string sql, Func<T, Task<bool>> onRead, string? continuationToken = null, int? maxItemCount = null, Dictionary<string, object>? parameters = null, CancellationToken cancelToken = default) {
			var pk = partitionKey != null ? new PartitionKey(partitionKey) : (PartitionKey?)null;
			return this.ReadSQL(pk, sql, onRead, continuationToken, maxItemCount, parameters, cancelToken);
		}
		/// <inheritdoc />
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

		/// <inheritdoc />
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

		/// <inheritdoc />
		public IAsyncEnumerable<JsonElement> RawSQL(string? partitionKey, string sql, Dictionary<string, object>? parameters = null, System.Threading.CancellationToken cancelToken = default) {
			var pk = partitionKey != null ? new PartitionKey(partitionKey) : (PartitionKey?)null;
			return RawSQL(pk, sql, parameters, cancelToken);
		}
		/// <summary>
		/// Executes raw SQL with hierarchical partition key.
		/// </summary>
		public IAsyncEnumerable<JsonElement> RawSQL(string[]? partitionKey, string sql, Dictionary<string, object>? parameters = null, System.Threading.CancellationToken cancelToken = default) {
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

		/// <summary>
		/// Performs bulk insert of items with automatic partition key extraction.
		/// </summary>
		/// <param name="items">Items to insert.</param>
		/// <param name="getPartitionKey">Function to extract partition key from item.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
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

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
	}


}
