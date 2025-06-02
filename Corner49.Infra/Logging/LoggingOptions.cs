using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Corner49.Infra.Logging {
	public class LoggingOptions {


		public LoggingOptions() {
			this.TrackDependencies = true;
			this.TrackContent = true;
			this.TrackLongRequestThreshold = 500;
		}

		public bool TrackDependencies { get; set; }
		public bool TrackContent { get; set; }

		public long TrackLongRequestThreshold { get; set; }

		public string[]? IngoreRequestsPaths { get; set; }	


		public bool WriteToConsoleAsJson { get; set; }
		public bool AzureWebAppDiagnostics { get; set; }

		


		/// <summary>
		/// Filter logging bases on category prefix
		/// </summary>
		public string[]? FilterCategoryPrefix { get; set; }


	}
}
