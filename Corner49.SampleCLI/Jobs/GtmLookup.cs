using Corner49.Infra;
using Corner49.Infra.ServiceBus;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace Corner49.SampleCLI.Jobs {

	public class GtmLookup : ServiceBusJob {
		private readonly ILogger<GtmLookup> _logger;



		public GtmLookup(ILogger<GtmLookup> logger, IServiceProvider serviceProvider) : base(serviceProvider) {
			_logger = logger;
			
			this.Queue = "gtm_lookup";
		}

		protected override async Task<bool> Run(ServiceBusCommand cmd, CancellationToken cancellationToken = default) {
			var name = cmd.GetId();


			Console.WriteLine(name);

			return true;
		}
	}
}
