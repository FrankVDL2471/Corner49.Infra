using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Corner49.Infra.ServiceBus {

	public interface IServiceBusService {

		bool DeveloperMode { get; }

		ServiceBusMessageSender GetSender(IServiceBusOptions opt);
		ServiceBusMessageSender GetQueueSender(string queueName, TimeSpan? duplicateDetection = null);
		ServiceBusMessageSender GetTopicSender(string queueName, TimeSpan? duplicateDetection = null);


		Task<ServiceBusProcessor> GetProcessor(IServiceBusOptions options);
		Task<ServiceBusProcessor> GetQueue(IServiceBusOptions options);
		Task<ServiceBusProcessor> GetSubscription(IServiceBusOptions options);

		ValueTask<bool> DeleteQueue(string? queueName);
		Task ResubmitDeadletterQueue(IServiceBusOptions options);


		Task<ServiceBusProcessor> StartProcessor(IServiceBusOptions options, Func<ProcessMessageEventArgs, Task> processMessage, Func<ProcessErrorEventArgs, Task> processErrors);
		ValueTask<bool> IsTopicFull(string name);


		Task<long?> GetMessageCount(IServiceBusOptions options);

	}
	public class ServiceBusService : IServiceBusService {

		private readonly ILogger<ServiceBusService> _logger;
		private readonly ServiceBusConfiguration _config;

		private readonly ServiceBusClient _client;
		private readonly ServiceBusAdministrationClient _admin;



		public ServiceBusService(ILogger<ServiceBusService> logger, IOptions<ServiceBusConfiguration> options) {
			_logger = logger;
			_config = options.Value;

			if (string.IsNullOrEmpty(_config.ConnectString)) {
				throw new ArgumentNullException("ServiceBusConfiguration.ConnectString", "ServiceBus connect string is not set, use InfraBuilder.AddServiceBus to configure connection");
			}

			_client = new ServiceBusClient(_config.ConnectString);
			_admin = new ServiceBusAdministrationClient(_config.ConnectString);

		}

		public bool DeveloperMode => _config.DeveloperMode;

		public async Task<ServiceBusProcessor> StartProcessor(IServiceBusOptions options, Func<ProcessMessageEventArgs, Task> processMessage, Func<ProcessErrorEventArgs, Task> processErrors) {

			string? connString = options.ConnectString ?? _config.ConnectString;
			if (string.IsNullOrEmpty(connString)) {
				throw new ArgumentNullException("ConnectString", "ServiceBusOptions.ConnectString is not set");
			}
			if (options.Name == null) {
				throw new ArgumentNullException("Name", "ServiceBusOptions.Name is not set");
			}

			var processor = await this.GetProcessor(options);
			if (processMessage != null) processor.ProcessMessageAsync += processMessage;
			if (processErrors != null) processor.ProcessErrorAsync += processErrors;

			await processor.StartProcessingAsync();
			return processor;
		}




		public Task<ServiceBusProcessor> GetProcessor(IServiceBusOptions options) {
			return (options.Kind == ServiceBusKind.Topic) ? GetSubscription(options) : GetQueue(options);
		}

		#region Queues

		private static readonly List<string> _knownQueues = new List<string>();
		private static readonly SemaphoreSlim _lockQueue = new SemaphoreSlim(1, 1);


		public async Task<ServiceBusProcessor> GetQueue(IServiceBusOptions options) {
			var nm = await GetQueue(options.Name, options.DuplicateDetectionWindow);

			ServiceBusProcessorOptions opt = new ServiceBusProcessorOptions();
			opt.MaxConcurrentCalls = options.MaxConcurrentCalls;
			opt.MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(30);
			opt.AutoCompleteMessages = true;
			opt.ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete;
			opt.PrefetchCount = options.PrefetchCount;
			if (options.DealLetter) {
				opt.SubQueue = SubQueue.DeadLetter;
			}
			return _client.CreateProcessor(nm, opt);
		}
		private async ValueTask<string> GetQueue(string name, TimeSpan? dupplicateDetection = null) {
			string queueName = name.ToLower();
			if (this.DeveloperMode) {
				queueName += "." + Environment.MachineName.ToLower();
				queueName = queueName.Length > 50 ? queueName.Substring(0, 50) : queueName;
			}

			if (_knownQueues.Contains(queueName)) return queueName;

			await _lockQueue.WaitAsync();
			try {
				try {
					var queue = await _admin.GetQueueAsync(queueName);
					if (queue != null) {
						_knownQueues.Add(queueName);
						return queueName;
					}
				} catch (ServiceBusException busErr) {
					if (busErr.Reason == ServiceBusFailureReason.MessagingEntityNotFound) {
						var opt = new CreateQueueOptions(queueName);
						opt.LockDuration = TimeSpan.FromMinutes(5);
						opt.DefaultMessageTimeToLive = TimeSpan.FromDays(7);
						opt.DeadLetteringOnMessageExpiration = true;
						opt.MaxDeliveryCount = _config.MaxDeliveryCount;

						if ((_config.DeveloperMode) && (!_config.IsBasicTier)) {
							opt.AutoDeleteOnIdle = TimeSpan.FromHours(12);
						}

						if ((dupplicateDetection != null) && (!_config.IsBasicTier)) {
							opt.RequiresDuplicateDetection = true;
							opt.DuplicateDetectionHistoryTimeWindow = dupplicateDetection.Value;
						} else {
							opt.RequiresDuplicateDetection = false;
						}


						var resp = await _admin.CreateQueueAsync(opt);
						_knownQueues.Add(queueName.ToLower());
						return queueName;
					}
				}

				return queueName;
			} catch (Exception er) {
				_logger.LogError(er, $"Create Queue {queueName} failed : {er.Message}");
				return queueName;
			} finally {
				_lockQueue.Release();
			}
		}

		public async ValueTask<bool> DeleteQueue(string? queueName) {
			if (string.IsNullOrEmpty(queueName)) return false;
			var nm = await GetQueue(queueName);
			if (_knownQueues.Contains(nm)) _knownQueues.Remove(nm);
			try {
				var resp = await _admin.DeleteQueueAsync(nm);
			} catch (Exception) {
				return false;
			}
			return true;
		}


		public async Task ResubmitDeadletterQueue(IServiceBusOptions options) {
			var nm = await GetQueue(options.Name, options.DuplicateDetectionWindow);

			ServiceBusProcessorOptions opt = new ServiceBusProcessorOptions();
			opt.MaxConcurrentCalls = options.MaxConcurrentCalls;
			opt.MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(30);
			opt.AutoCompleteMessages = true;
			opt.ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete;
			opt.PrefetchCount = options.PrefetchCount;
			opt.SubQueue = SubQueue.DeadLetter;

			var proc =  _client.CreateProcessor(nm, opt);
			proc.ProcessMessageAsync += async (args) => {
				var sender = _client.CreateSender(nm);

				var msg =   new ServiceBusMessage(args.Message.Body);
				msg.MessageId = args.Message.MessageId;
				msg.Subject = args.Message.Subject;
				msg.To = args.Message.To;					
				foreach(var prop in args.Message.ApplicationProperties) {
					if (msg.ApplicationProperties.ContainsKey(prop.Key)) {
						msg.ApplicationProperties[prop.Key] = prop.Value;
					} else {
						msg.ApplicationProperties.Add(prop.Key, prop.Value);	
					}					
				}	
				await sender.SendMessageAsync(msg);
			};


			await proc.StartProcessingAsync();

			while(true) {
				var cnt = await this.GetMessageCount(options);
				if (cnt == 0) break;
				await Task.Delay(500);
			}

			await proc.StopProcessingAsync();

		}


		#endregion

		#region Topics



		private static readonly List<string> _knownTopics = new List<string>();
		private static readonly SemaphoreSlim _lockTopic = new SemaphoreSlim(1, 1);


		public async ValueTask<bool> IsTopicFull(string name) {
			string topicName = name.ToLower();
			try {
				var topic = await _admin.GetTopicAsync(topicName);
				var prp = await _admin.GetTopicRuntimePropertiesAsync(topicName);

				var size = topic.Value.MaxSizeInMegabytes - prp.Value.SizeInBytes / (1024 * 1024);
				if (size < 10) return true; //10MB available space
			} catch {
			}
			return false;
		}


		private async ValueTask<string> GetTopic(string name, bool createIfNotExists = true, TimeSpan? dupplicateDetection = null) {
			string topicName = name.ToLower();

			if (_knownTopics.Contains(topicName)) return topicName;

			await _lockTopic.WaitAsync();
			try {
				try {
					var topic = await _admin.GetTopicAsync(topicName);
					if (topic != null) {
						_knownTopics.Add(topicName);
						return topicName;
					}
				} catch (ServiceBusException busErr) {
					if (busErr.Reason == ServiceBusFailureReason.MessagingEntityNotFound) {
						if (!createIfNotExists) return null;

						var opt = new CreateTopicOptions(topicName);
						opt.AutoDeleteOnIdle = TimeSpan.FromDays(30);
						opt.DefaultMessageTimeToLive = TimeSpan.FromDays(7);
						opt.MaxSizeInMegabytes = 5120;

						if (dupplicateDetection != null) {
							opt.RequiresDuplicateDetection = true;
							opt.DuplicateDetectionHistoryTimeWindow = dupplicateDetection.Value;
						} else {
							opt.RequiresDuplicateDetection = false;
						}

						var resp = await _admin.CreateTopicAsync(opt);
						_knownTopics.Add(topicName);
						return topicName;
					}
				}

				return topicName;
			} catch (Exception err) {
				_logger.LogError(err, $"CreateTopic {topicName} failed : {err.Message}");
				return topicName;
			} finally {
				_lockTopic.Release();
			}
		}

		private async ValueTask<string> GetSubscription(string topicName, string subscriptionName, string? subscriptionFilter = null) {
			if (_config.DeveloperMode) {
				subscriptionName += "." + Environment.MachineName.ToLower();
				subscriptionName = subscriptionName.Length > 50 ? subscriptionName.Substring(0, 50) : subscriptionName;
			}

			string fullName = $"{topicName}.{subscriptionName}";

			if (_knownTopics.Contains(fullName)) return subscriptionName;

			bool createNewSubscription = false;
			await _lockTopic.WaitAsync();
			try {
				try {
					var sub = await _admin.GetSubscriptionAsync(topicName, subscriptionName);
					if (sub != null) {
						if (subscriptionFilter != null) {
							//Check if filter is changed;
							var rules = _admin.GetRulesAsync(topicName, subscriptionName);
							await Parallel.ForEachAsync(rules, async (rule, ct) => {
								if (rule.Filter is SqlRuleFilter sql) {
									if (sql.SqlExpression != subscriptionFilter) {
										createNewSubscription = true;
									}
								}
							});
						}
						if (createNewSubscription) {
							await _admin.DeleteSubscriptionAsync(topicName, subscriptionName);
						} else {
							_logger.LogInformation($"GetSubscription ({topicName}, {subscriptionName})");
							_knownTopics.Add(fullName);
							return subscriptionName;
						}
					} else {
						_logger.LogWarning($"GetSubscription ({topicName}, {subscriptionName}) failed : not found");
					}

				} catch (ServiceBusException busErr) {
					if (busErr.Reason == ServiceBusFailureReason.MessagingEntityNotFound) {
						_logger.LogInformation($"Create SubScription ({topicName}, {subscriptionName})");

						createNewSubscription = true;
					}
				}

				if (createNewSubscription) {
					_logger.LogWarning($"Create Subscription {topicName} : {subscriptionName}");

					var opt = new CreateSubscriptionOptions(topicName, subscriptionName);
					opt.AutoDeleteOnIdle = _config.DeveloperMode ? TimeSpan.FromHours(1) : TimeSpan.FromDays(30);
					opt.DefaultMessageTimeToLive = _config.DeveloperMode ? TimeSpan.FromHours(1) : TimeSpan.FromDays(7);
					opt.MaxDeliveryCount = _config.MaxDeliveryCount;
					opt.LockDuration = TimeSpan.FromMinutes(5);
					opt.DeadLetteringOnMessageExpiration = true;

					if (subscriptionFilter != null) {
						var resp = await _admin.CreateSubscriptionAsync(opt, new CreateRuleOptions { Filter = new SqlRuleFilter(subscriptionFilter) });
					} else if (this.DeveloperMode) {
						var filter = new CorrelationRuleFilter();
						var resp = await _admin.CreateSubscriptionAsync(opt, new CreateRuleOptions { Filter = new SqlRuleFilter($"Target = '{System.Environment.MachineName}'") });
					} else {
						var resp = await _admin.CreateSubscriptionAsync(opt);
					}
					_knownQueues.Add(fullName);
				}

				return subscriptionName;
			} catch (Exception err) {
				_logger.LogError(err, $"CreateSubscription {fullName} failed : {err.Message}");
				return subscriptionName;
			} finally {
				_lockTopic.Release();
			}
		}

		public async Task<ServiceBusProcessor> GetSubscription(IServiceBusOptions options) {
			var topic = await GetTopic(options.Name, true, options.DuplicateDetectionWindow);
			var sub = await GetSubscription(topic, options.SubscriptionName, options.SubscriptionFilter);

			ServiceBusProcessorOptions opt = new ServiceBusProcessorOptions();
			opt.MaxConcurrentCalls = options.MaxConcurrentCalls;
			opt.MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(30);
			opt.PrefetchCount = options.PrefetchCount;

			return _client.CreateProcessor(topic, sub, opt);
		}


		#endregion



		public async Task<long?> GetMessageCount(IServiceBusOptions options) {
			try {
				if (options.Kind == ServiceBusKind.Queue) {
					string queueName = options.Name.ToLower();
					if (this.DeveloperMode) {
						queueName += "." + Environment.MachineName.ToLower();
						queueName = queueName.Length > 50 ? queueName.Substring(0, 50) : queueName;
					}
					var resp = await _admin.GetQueueRuntimePropertiesAsync(queueName);
					return resp?.Value?.TotalMessageCount;
				} else {
					var name = options.SubscriptionName;
					if (_config.DeveloperMode) {
						name += "." + Environment.MachineName.ToLower();
						name = name.Length > 50 ? name.Substring(0, 50) : name;
					}
					var resp = await _admin.GetSubscriptionRuntimePropertiesAsync(options.Name, name);
					return resp?.Value?.TotalMessageCount;
				}

			} catch (Exception er) {
				return null;
			}
		}


		public ServiceBusMessageSender GetSender(IServiceBusOptions opt) {
			if (string.IsNullOrEmpty(opt?.Name)) throw new ArgumentNullException("opt.Name");
			if (opt.Kind == ServiceBusKind.Queue) {
				return GetQueueSender(opt.Name, opt.DuplicateDetectionWindow);
			} else {
				return GetTopicSender(opt.Name, opt.DuplicateDetectionWindow);
			}
		}

		public ServiceBusMessageSender GetQueueSender(string name, TimeSpan? duplicateDetection = null) {
			return new ServiceBusMessageSender(_client, _logger, this.DeveloperMode, async () => {
				var nm = await GetQueue(name, duplicateDetection);
				return _client.CreateSender(nm);
			});
		}
		public ServiceBusMessageSender GetTopicSender(string name, TimeSpan? duplicateDetection = null) {
			return new ServiceBusMessageSender(_client, _logger, this.DeveloperMode, async () => {
				var nm = await GetTopic(name, true, duplicateDetection);
				return _client.CreateSender(nm);
			});
		}



	}




}
