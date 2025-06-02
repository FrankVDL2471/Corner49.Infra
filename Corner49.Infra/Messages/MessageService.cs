using Corner49.Infra.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Corner49.Infra.Messages {

	public interface IMessageService<T> where T : MessageBase {
		Task Send(T msg, DateTimeOffset? enqueueTime = null);
		Task Send(T msg, int throttleDelay);
	}

	public abstract class MessageService<T> : IMessageService<T> where T : MessageBase {
		protected readonly ILogger _logger;
		protected readonly IServiceBusService _serviceBus;

		public MessageService(ILogger logger, IServiceProvider serviceProvider) {
			_logger = logger;
			_serviceBus = serviceProvider.GetService<IServiceBusService>();
		}


		private ServiceBusMessageSender GetSender(MessageBase msg) {
			return msg.UseQueue? _serviceBus.GetQueueSender(msg.Name, TimeSpan.FromSeconds(20)) : _serviceBus.GetTopicSender(msg.Name, TimeSpan.FromSeconds(20));
		}

		public Task Send(T msg, DateTimeOffset? enqueueTime = null) {
			if (msg?.Action == null) throw new ArgumentNullException("msg.Action", "Action is not set");

			var cmd = CreateServiceBusCommand(msg, enqueueTime);

			var sender = this.GetSender(msg);
			return sender.Send(cmd);
		}

		private Dictionary<string, DateTimeOffset> _throttle = new Dictionary<string, DateTimeOffset>();

		public async Task Send(T msg, int throttleDelay) {
			if (msg?.Action == null) throw new ArgumentNullException("msg.Action", "Action is not set");

			var cmd = CreateServiceBusCommand(msg, null);

			if (!string.IsNullOrEmpty(cmd.MessageId)) {
				DateTimeOffset? delay = null;
				lock (_throttle) {
					if (_throttle.ContainsKey(cmd.MessageId)) {
						delay = _throttle[cmd.MessageId];
						if (delay < DateTimeOffset.UtcNow) {
							delay = DateTimeOffset.UtcNow.AddSeconds(throttleDelay);
							_throttle[cmd.MessageId] = delay.Value;
						} else {
							Console.WriteLine("SKIP - " + cmd.MessageId);
							return;  //message is throttled, do not send to queue
						}
					} else {
						delay = DateTimeOffset.UtcNow.AddSeconds(throttleDelay);
						_throttle.Add(cmd.MessageId, delay.Value);
					}
				}
				if (delay != null) {
					await Task.Delay((int)delay.Value.Subtract(DateTimeOffset.UtcNow).TotalMilliseconds);
				}

			}

			var sender = this.GetSender(msg);
			await sender.Send(cmd);
		}



		public Task SendBulk(IEnumerable<T> msgs, DateTimeOffset? enqueueTime = null) {
			if (!msgs.Any()) return Task.CompletedTask;
			if (msgs.Any(m => m.Action == null)) throw new ArgumentNullException("msg.Action", "Action is not set");

			var cmds = msgs.Select(m => CreateServiceBusCommand(m, enqueueTime)).ToList();

			var sender = this.GetSender(msgs.FirstOrDefault());
			return sender.Send(cmds);
		}

		private static ServiceBusCommand CreateServiceBusCommand(T msg, DateTimeOffset? enqueueTime = null) {
			ServiceBusCommand cmd = new ServiceBusCommand();
			cmd.Name = msg.Action;
			cmd.SetData((object)msg);
			cmd.Timestamp = enqueueTime;

			var msgId = msg.GetMessageId();
			if (msgId != null) {
				cmd.MessageId = msgId;
			}
			return cmd;
		}
	}
}
