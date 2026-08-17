using Corner49.Infra.DB;
using Corner49.Sample.Models;
using Microsoft.Azure.Cosmos;
using System.Xml.Linq;


namespace Corner49.Sample.Repos {
	
	public interface IDataRepo  {

		Task<DataModel?> GetItem(string pk, string id);

		Task<QueryResult<DataModel>> Query(Func<IQueryable<DataModel>, IQueryable<DataModel>> query);

		Task ImportBatch(IEnumerable<DataModel> items);
	}

	public class DataRepo : IDataRepo, IDocumentRepoInitializer {

		private readonly IDocumentDB _db;
		private readonly DocumentRepo<DataModel> _repo;

		public DataRepo(IDocumentDB db) {
			_db = db;
			_repo = _db.GetRepo<DataModel>($"dev-Ottogusto", "Parts", "partitionKey");

			_repo.OnDiagnostics = (diag) => this.OnDiagnostics("Parts", diag);

		}

		private Task OnDiagnostics(string repo, DocumentDiagnostics diag) {
			string args = string.Join(", ", diag.Parameters?.Select(c => $"{c.Key} = '{c.Value}'"));


			Console.WriteLine($"Diagnostics {repo} : {diag.Method} ({args}) - {diag.ElapsedTime?.TotalMilliseconds} msec, {diag.TotalRequestCharge} RUs");
			return Task.CompletedTask;
		}


		Task IDocumentRepoInitializer.Init() {
			return _repo.Init();
		}

		public Task<DataModel?> GetItem(string pk, string id) {
			return _repo.GetItem(pk, id);
		}


		public Task<QueryResult<DataModel>> Query(Func<IQueryable<DataModel>, IQueryable<DataModel>> query) {
			return _repo.Query((string?)null, query);
		}

		// Example of an RU-intensive job pausing itself before it starts getting 429-throttled,
		// instead of just retrying reactively after the fact.
		public async Task ImportBatch(IEnumerable<DataModel> items) {
			foreach (var item in items) {
				await _repo.WaitForCapacity(pressureThreshold: 0.8);
				await _repo.UpsertItem(item.PartitionKey, item);
			}
		}

	}
}
