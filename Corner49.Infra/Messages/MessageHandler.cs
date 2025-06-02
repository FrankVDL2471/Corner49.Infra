using Corner49.Infra.ServiceBus;

namespace Corner49.Infra.Messages {

	public class MessageHandler<T> : IServiceBusHandler where T : MessageBase {

		public MessageHandler() {
		}

		public virtual async Task MessageReceived(ServiceBusCommand cmd) {
			object msg = cmd.GetData() ?? cmd.GetData<T>();

			await Process(cmd.Name, (T)msg);

			var method = GetType().GetMethod(cmd.Name, new Type[] { msg.GetType() });
			if (method != null) {
				await (Task)method.Invoke(this, new object[] { msg });
			}
		}

		public virtual Task<bool> Process(string action, T message) {
			return Task.FromResult(true);
		}

	}
}
