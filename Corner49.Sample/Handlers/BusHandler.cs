using Corner49.ServiceBus;
using Corner49.ServiceBus.Bus;

namespace Corner49.Sample.Handlers {
	public class BusHandler : IServiceBusHandler {
		public Task MessageReceived(ServiceBusCommand msg) {
			Console.WriteLine($"ServiceBusCommand : {msg.Name}");	
			return Task.CompletedTask;
		}
	}
}
