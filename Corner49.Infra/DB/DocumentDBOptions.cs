using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Corner49.Infra.DB {
	public class DocumentDBOptions {


		public static string SectionName = "CosmosDB";
		public DocumentDBOptions() {
			this.DatabaseName = "data";
		}

		public string? ConnectString { get; set; }

		/// <summary>
		/// The default database name for all the repos
		/// </summary>
		public string DatabaseName { get; set; }

		public bool DirectMode { get; set; }
	}
}
