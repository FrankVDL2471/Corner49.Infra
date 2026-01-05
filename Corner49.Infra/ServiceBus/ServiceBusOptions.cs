#nullable enable

namespace Corner49.Infra.ServiceBus {


	public enum ServiceBusKind {
		Topic,
		Queue,
	}

	public interface IServiceBusOptions {


		/// <summary>
		/// Queue or Topic
		/// Default = Topic
		/// </summary>
		ServiceBusKind Kind { get; set; }


		/// <summary>
		/// Name of Queue or Topic
		/// If not set the infra name is used
		/// </summary>
		string? Name { get; set; }



		/// <summary>
		/// The amount of simulatanuos threads are watching the servicebus
		/// Default = 30
		/// </summary>
		int MaxConcurrentCalls { get; set; }


		/// <summary>
		/// Set a custom connectstring for a ServiceBus Namespace. 
		/// If not set the ServiceBus in the current environment will be used
		/// </summary>
		string? ConnectString { get; set; }

		string SubscriptionName { get; set; }

		/// <summary>
		/// Sets a SQL filter on the subscription
		/// </summary>
		string? SubscriptionFilter { get; set; }

		/// <summary>
		/// Enable Duplicate detection with the this window size
		/// Min value : 20 sec
		/// Max value : 7 days 
		/// </summary>
		TimeSpan? DuplicateDetectionWindow { get; set; }


		/// <summary>
		/// Gets or sets the number of messages that will be eagerly requested from Queues or Subscriptions and queued locally, intended to help maximize throughput by allowing the processor to receive from a local cache rather than waiting on a service request.
		/// Default = 0
		/// </summary>
		int PrefetchCount { get; set; }


		bool TrackMessageCount { get; set; }

		bool DealLetter { get; set; }
	}

	public class ServiceBusOptions : IServiceBusOptions {

		public ServiceBusOptions(string subscriptionName) {
			this.SubscriptionName = subscriptionName;
			this.Kind = ServiceBusKind.Queue;
			this.MaxConcurrentCalls = 30;		
			this.SubscriptionFilter = null;
			this.DealLetter = false;
		}

		public ServiceBusKind Kind { get; set; }

		public string? Name { get; set; }


		public int MaxConcurrentCalls { get; set; }

		public string? ConnectString { get; set; }

		public string SubscriptionName { get; set; }
		public string? SubscriptionFilter { get; set; }
				
		public TimeSpan? DuplicateDetectionWindow { get; set; }
		
		public int PrefetchCount { get; set; }

		public bool TrackMessageCount { get; set; }

		public bool DealLetter { get; set; }
	}

}
