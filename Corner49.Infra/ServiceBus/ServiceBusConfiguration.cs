using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Corner49.Infra.ServiceBus {
	public class ServiceBusConfiguration {

		public static string SectionName = "ServiceBus";

		public ServiceBusConfiguration() {
			this.MaxDeliveryCount = 10;
		}

		public string? ConnectString { get; set; }


		/// <summary>
		/// Enable developermode
		/// - MaxConcurrentCalls will be set to 1
		/// - For a Queue a temporary queue will be created based on your machine name
		/// - For a Topic a subscription filtering out only your messages will be created
		/// </summary>
		public bool DeveloperMode { get; set; }


		/// <summary>
		/// Max DeliveryCount
		/// Default = 10, Minimal = 1
		/// </summary>
		public int MaxDeliveryCount { get; set; }	


		/// <summary>
		/// Is servicebus configured as basic tier
		/// </summary>
		public bool IsBasicTier { get; set; }
	}
}
