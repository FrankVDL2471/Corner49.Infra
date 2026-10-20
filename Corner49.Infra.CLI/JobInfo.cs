using System;
using System.Collections.Generic;
using System.Text;

namespace Corner49.Infra {
	internal record JobInfo {

		public string? Name { get; set; }

		public string? Description { get; set; }	

		public Type? ServiceType { get; set; }
	}
}
