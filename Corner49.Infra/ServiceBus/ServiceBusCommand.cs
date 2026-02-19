using Azure.Messaging.ServiceBus;
using System.Text.Json;

#nullable enable

namespace Corner49.Infra.ServiceBus {
	public class ServiceBusCommand {

		/// <summary>
		/// Unique id used for duplication detection
		/// </summary>
		public string? MessageId { get; set; }

		/// <summary>
		/// Command to execute (mapped to ServiceBusMessage.subject)
		/// </summary>
		public string? Name { get; set; }

		/// <summary>
		/// If not set in the message, the enqueded time will be used
		/// </summary>
		public DateTimeOffset? Timestamp { get; set; }


		/// <summary>
		/// Optional partitionKey to improve service performance
		/// (mapped to ServiceBusMessage.PartitionKey)
		/// </summary>
		public string? PartitionKey { get; set; }

		/// <summary>
		/// Source trigger of the data/message
		/// (mapped to ServiceBusMessage.ApplicationProperties["Source"])
		/// </summary>
		public string? Source { get; set; }


		/// <summary>
		/// Can be used to send message to specfic target (mapped to ServiceBusMessage.To)
		/// Subscription can be filtered on this target
		/// </summary>
		public string? Target { get; set; }


		public BinaryData? Data { get; set; }

		public Type? DataType { get; set; }


		/// <summary>
		/// Number of messages that still need to be processed
		/// </summary>
		public long? MessageCount { get; set; }


		public T? GetData<T>() where T : class {
			try {
				if (Data == null) return null;
				if (Data.IsEmpty) return null;
				return Data.ToObjectFromJson<T>(JsonOptions);
			} catch { 
				return null; 
			}
		}

		public object? GetData() {
			if (DataType == null) return null;
			if (Data?.IsEmpty == true) return null;
			return JsonSerializer.Deserialize(Data.ToStream(), DataType, JsonOptions);
		}

		public Stream GetStream() {
			if (Data == null) return null;
			return Data.ToStream();
		}

		public string GetBody() {
			if (Data == null) return null;
			return Data.ToString();
		}


		public void SetData(object itm) {
			DataType = itm.GetType();
			string json = JsonSerializer.Serialize(itm, itm.GetType(), JsonOptions);
			Data = BinaryData.FromString(json);
		}
		public void SetData<T>(T itm) {
			DataType = typeof(T);
			Data = BinaryData.FromObjectAsJson(itm, JsonOptions);
		}
		public void SetData(Stream data) {
			if (data == null) return;
			if (data.Length == 0) return;
			Data = BinaryData.FromStream(data);
		}
		public void SetData(string data) {
			Data = BinaryData.FromString(data);
		}


		private JsonSerializerOptions JsonOptions => new JsonSerializerOptions {
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
		};



		public static ServiceBusCommand GetCommand(ProcessMessageEventArgs arg) {
			return GetCommand(arg.Message);
		}

		public static ServiceBusCommand GetCommand(ServiceBusReceivedMessage message) {
			ServiceBusCommand msg = new ServiceBusCommand();
			msg.Name = message.Subject;
			msg.Target = message.ApplicationProperties.ContainsKey("Target") ? message.ApplicationProperties["Target"] as string : null;
			msg.Source = message.ApplicationProperties.ContainsKey("Source") ? message.ApplicationProperties["Source"] as string : null;
			if (message.EnqueuedTime != default) {
				msg.Timestamp = message.EnqueuedTime;
			} else if (message.ApplicationProperties.ContainsKey("Timestamp")) {
				string ts = (string)message.ApplicationProperties["Timestamp"];
				long epoch = 0;
				if (long.TryParse(ts, out epoch)) {
					msg.Timestamp = DateTimeOffset.FromUnixTimeSeconds(epoch);
				}
			}
			if (message.ApplicationProperties.ContainsKey("DataType")) {
				string[] dataType = ((string)message.ApplicationProperties["DataType"]).Split(',');
				msg.DataType = Type.GetType($"{dataType[0]}, {dataType[1]}", false);  //only use class and assembly name, ignore version
			}
			msg.Data = message.Body;
			return msg;

		}
	}
}
