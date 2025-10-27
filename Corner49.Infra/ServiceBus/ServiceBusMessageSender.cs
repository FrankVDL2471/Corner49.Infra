using Azure.Messaging.EventGrid;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;

namespace Corner49.Infra.ServiceBus {
	public class ServiceBusMessageSender {


		private readonly ServiceBusClient _client;
		private readonly ILogger _logger;
		private readonly Func<Task<ServiceBusSender>> _creator;
		private readonly bool _developerMode;

		internal ServiceBusMessageSender(ServiceBusClient client, ILogger<ServiceBusService> logger, bool developerMode, Func<Task<ServiceBusSender>> createSender) {
			_client = client;
			_logger = logger;
			_developerMode = developerMode;
			_creator = createSender;
		}

		public Task<ServiceBusSender> GetSender() {
			return _creator.Invoke();
		}

		public async Task Send(List<ServiceBusCommand> commands) {
			var sender = await GetSender();

			var index = 0;
			while (index < commands.Count) {
				using var batch = await sender.CreateMessageBatchAsync();
				while (index < commands.Count) {
					var msg = CreateMessage(commands[index]);
					var success = batch.TryAddMessage(msg);
					if (!success || index == commands.Count - 1) {
						try {
							await sender.SendMessagesAsync(batch);
							if (success) {
								index++;
							}
							break;
						} catch {
							await sender.DisposeAsync();
							throw;
						}
					} else {
						index++;
					}
				}
			}
			await sender.DisposeAsync();
		}


		public async Task Send(ServiceBusCommand cmd) {
			var sender = await GetSender();
			try {
				var msg = CreateMessage(cmd);
				if (cmd.Timestamp != null && cmd.Timestamp > DateTime.UtcNow) {
					//_logger.LogInformation($"Send delayed message {msg.MessageId}:{cmd.Name} at {cmd.Timestamp.Value.LocalDateTime} on {sender.EntityPath}");
					await sender.ScheduleMessageAsync(msg, cmd.Timestamp.Value);
				} else {
					//_logger.LogInformation($"Send message {msg.MessageId}:{cmd.Name} on {sender.EntityPath}");
					await sender.SendMessageAsync(msg);
				}
			} finally {
				await sender.DisposeAsync();
			}

		}


		/// <summary>
		/// Adds a scheduled message to the services bus queue.
		/// Scheduled messages do not materialize in the queue until the defined enqueue time. 
		/// When the message is received by the listener depends on the workload on the queue
		/// </summary>
		/// <param name="cmd">PubSubMessage command to add to the queue</param>
		/// <param name="scheduleTime">UTC Timestamp when the message is scheduled to be added to the queue. </param>
		/// <returns>Unique SequenceNumber that can be used to cancel the message</returns>
		public async Task<long> Send(ServiceBusCommand cmd, DateTimeOffset scheduleTime) {
			var sender = await GetSender();
			try {

				if (cmd.Timestamp == null) cmd.Timestamp = scheduleTime;
				var msg = CreateMessage(cmd);
				return await sender.ScheduleMessageAsync(msg, scheduleTime);
			} finally {
				await sender.DisposeAsync();
			}
		}

		/// <summary>
		/// Cancel an scheduled mesage before it's added to the queue
		/// </summary>
		/// <param name="sequenceNumber"></param>
		/// <returns></returns>
		public async Task<bool> Cancel(long sequenceNumber) {
			var sender = await GetSender();
			try {
				await sender.CancelScheduledMessageAsync(sequenceNumber);
				return true;
			} catch (ServiceBusException busErr) {
				if (busErr.Reason == ServiceBusFailureReason.MessageNotFound) return true;
				return false;
			} catch (Exception err) {
				return false;
			} finally {
				await sender.DisposeAsync();
			}
		}



		private ServiceBusMessage CreateMessage(ServiceBusCommand cmd) {
			ServiceBusMessage msg;
			if (cmd.Data != null) {
				msg = new ServiceBusMessage(cmd.Data);
			} else {
				msg = new ServiceBusMessage();
			}
			msg.Subject = cmd.Name;

			if ((_developerMode) && (cmd.Target == null)) {
				cmd.Target = System.Environment.MachineName;
			}

			if (cmd.MessageId != null) msg.MessageId = cmd.MessageId;
			if (cmd.Source != null) msg.ApplicationProperties.Add("Source", cmd.Source);
			if (cmd.Target != null) msg.ApplicationProperties.Add("Target", cmd.Target);
			if (cmd.Timestamp != null) msg.ApplicationProperties.Add("Timestamp", cmd.Timestamp.Value.ToUnixTimeMilliseconds().ToString());
			if (cmd.PartitionKey != null) msg.PartitionKey = cmd.PartitionKey;
			if (cmd.DataType != null) msg.ApplicationProperties.Add("DataType", cmd.DataType.AssemblyQualifiedName);
			var usr = Environment.UserName?.ToLower();
			if (usr != null) msg.ApplicationProperties.Add("User", usr);


			return msg;
		}

	}




}
