using Corner49.CLI.Jobs;
using System;
using System.Collections.Generic;
using System.Text;

namespace Corner49.SampleCLI.Jobs {
	internal class HelloWorldJob : IJob {

		public HelloWorldJob(IServiceProvider services) {

		}

		public async Task Execute(string[]? args = null, CancellationToken cancellationToken = default) {
			Console.WriteLine("Hello World!");
		}	
	}
}
