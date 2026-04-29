using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Corner49.Infra.DB {
	public class DocumentDiagnostics {

		public string? Repo { get; set; }

		public string? Method { get; set;  }
		public Dictionary<string, object?>? Parameters { get; set; }

		public HttpStatusCode? StatusCode { get; set;  }

		public DateTime? StartTime { get; set; }
		public TimeSpan? ElapsedTime { get; set; }

		public double? TotalRequestCharge { get; set; }

	}
}
