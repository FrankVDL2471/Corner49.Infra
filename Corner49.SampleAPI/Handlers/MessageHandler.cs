using Corner49.Infra.Messages;
using Corner49.Infra.ServiceBus;

namespace Corner49.SampleAPI.Handlers {
	public class MessageHandler : IServiceBusHandler {

		private readonly ILogger<MessageHandler> _logger;
		public MessageHandler(ILogger<MessageHandler> logger) {
			_logger = logger;
		}

		public Task MessageReceived(ServiceBusCommand msg) {
			_logger.LogInformation($"Message Received : {msg.Name}, source={msg.Source},target={msg.Target},timestamp={msg.Timestamp} ");
			return Task.CompletedTask; 
		}
	}

	public class TestMessage : MessageBase {

		public TestMessage() : base("Test", true) {
		}
	}

	public class TestMessageHandler : MessageHandler<TestMessage> {
		public TestMessageHandler(ILogger<TestMessageHandler> logger)  {
		}
		public override Task<bool> Process(string action, TestMessage message) {
			Console.WriteLine($"TestMessageHandler: {action}");
			return Task.FromResult(true);
		}
	}	
}
