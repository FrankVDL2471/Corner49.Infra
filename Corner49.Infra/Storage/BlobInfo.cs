using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Corner49.Infra.Storage {
	public class BlobInfo {

		public string? ContentType { get; set; }

		public string? ETag { get; set;  }

		public long? ContentLength { get; set;  }

		public DateTimeOffset? Date { get; set; }
	}
}
