using System;
using System.Collections.Generic;
using System.Text;

namespace Corner49.Infra.Models {
	internal class ChangeLease {

		public string Id { get; set; }
		public DateTime StartTime { get; set; }


		public string? ContinuationToken { get; set; }


	}
}
