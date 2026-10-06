using Corner49.ServiceBus.Bus;

namespace Corner49.ServiceBus {
	public interface IServiceBusHandler {
		Task MessageReceived(ServiceBusCommand msg);
	}
}
