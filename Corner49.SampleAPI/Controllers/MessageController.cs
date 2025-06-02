using Corner49.Infra.ServiceBus;
using Microsoft.AspNetCore.Mvc;

namespace Corner49.SampleAPI.Controllers {
	[ApiController]
	[Route("[controller]")]
	public class MessageController : ControllerBase {

		private readonly ILogger<MessageController> _logger;
		private readonly IServiceBusService _serviceBus;

		public MessageController(ILogger<MessageController> logger, IServiceBusService serviceBus) {
			_logger = logger;
			_serviceBus = serviceBus;
		}

		[EndpointSummary("Generate Messages")]
		[HttpPut("{cnt}")]
		public async Task<IActionResult> Generate(int cnt = 1) {

			var sender = _serviceBus.GetTopicSender("topictest");

			for(int i = 0; i < cnt; i ++) {
				ServiceBusCommand cmd = new ServiceBusCommand();
				cmd.Name = "Generated";
				cmd.Source = "SampleAPI";
				cmd.Target = "All";
				cmd.SetData(System.Guid.NewGuid().ToString());
				await sender.Send(cmd);
			}
			return Ok();
		}
	}
}
