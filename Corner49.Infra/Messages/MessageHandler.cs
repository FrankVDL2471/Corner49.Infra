using Corner49.Infra.ServiceBus;
using System.Security.Cryptography;

namespace Corner49.Infra.Messages {

	public class MessageHandler<T> : IServiceBusHandler where T : MessageBase {

		public MessageHandler() {
		}

		public virtual async Task MessageReceived(ServiceBusCommand cmd) {
			object msg = cmd.GetData() ?? cmd.GetData<T>();

			await Process(cmd.Name ?? cmd.GetType().Name, (T)msg);

			var method = GetType().GetMethod(cmd.Name ?? cmd.GetType().Name, new Type[] { msg.GetType() });
			if (method != null) {
				var task = method.Invoke(this, new object[] { msg }) as Task;
				if (task != null) {
					await task.ConfigureAwait(false);	
				}
			}
		}

		public virtual Task<bool> Process(string action, T message) {
			return Task.FromResult(true);
		}

	}
}
