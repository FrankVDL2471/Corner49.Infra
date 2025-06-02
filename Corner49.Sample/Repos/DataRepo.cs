using Corner49.Infra.DB;
using Corner49.Sample.Models;
using Microsoft.Azure.Cosmos;

namespace Corner49.Sample.Repos {
	
	public interface IDataRepo {

		Task<QueryResult<DataModel>> Query(Func<IQueryable<DataModel>, IQueryable<DataModel>> query);
	}

	public class DataRepo : IDataRepo, IDocumentRepoInitializer {

		private readonly IDocumentDB _db;
		private readonly DocumentRepo<DataModel> _repo;

		public DataRepo(IDocumentDB db) {
			_db = db;
			_repo = _db.GetRepo<DataModel>("data", "data", "name");
		}

		Task IDocumentRepoInitializer.Init() {
			return _repo.Init();
		}

		public Task<QueryResult<DataModel>> Query(Func<IQueryable<DataModel>, IQueryable<DataModel>> query) {
			return _repo.Query(null, query);
		}

	}
}
