using Corner49.Infra.ServiceBus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Corner49.Infra.Jobs {
	public class JobInfo {

		public string? Id { get; set; }

		public string? Type { get; set; }	

		public string? Name { get; set; }
		public Dictionary<string, string>? Args { get; set; }
		public string? Status { get; set; }

	}
}
