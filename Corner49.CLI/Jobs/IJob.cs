using System;
using System.Collections.Generic;
using System.Text;

namespace Corner49.CLI.Jobs {
	public interface IJob {

		public Task Execute(string[]? args = null, CancellationToken cancellationToken = default);
	}
}
