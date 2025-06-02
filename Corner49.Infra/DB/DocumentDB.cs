using Corner49.Infra.Tools;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Corner49.Infra.DB {

	public interface IDocumentDB {


		DocumentRepo<T> GetRepo<T>(string dbName, string tblName, string paritionKey) where T : class;
		DocumentRepo<T> GetRepo<T>(string tblName, string paritionKey) where T : class;

	}

	public class DocumentDB : IDocumentDB {

		private CosmosClient _client;
		private readonly DocumentDBOptions _options;

		public DocumentDB(IOptions<DocumentDBOptions> options)  {
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
	

			

		private void  Connect() {
			CosmosClientOptions options = new CosmosClientOptions();
			options.UseSystemTextJsonSerializerWithOptions = JsonHelper.Options;
			options.ConnectionMode = _options.DirectMode ? ConnectionMode.Direct : ConnectionMode.Gateway;

			if (options.ConnectionMode == ConnectionMode.Direct) {
				options.IdleTcpConnectionTimeout = TimeSpan.FromHours(1);
			}
			options.AllowBulkExecution = true;

			_client = new CosmosClient(_options.ConnectString, options);
		}


		public DocumentRepo<T> GetRepo<T>(string dbName, string tblName, string paritionKey) where T : class {
			return new DocumentRepo<T>(_client, dbName, tblName, paritionKey);
		}


		public DocumentRepo<T> GetRepo<T>(string tblName, string paritionKey) where T : class {
			return new DocumentRepo<T>(_client, _options.DatabaseName, tblName, paritionKey);
		}





	}
}
