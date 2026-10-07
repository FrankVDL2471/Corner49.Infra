namespace Corner49.Infra.ServiceBus {
	public interface IServiceBusHandler {
		Task MessageReceived(ServiceBusCommand msg);
	}
}
