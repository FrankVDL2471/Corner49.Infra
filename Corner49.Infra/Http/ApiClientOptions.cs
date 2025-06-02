using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Corner49.Infra.Http {
	public class ApiClientOptions {

		public Action<JsonSerializerOptions> JsonOptions { get; set; }

		public int? RateLimit { get; set; }


	}
}
