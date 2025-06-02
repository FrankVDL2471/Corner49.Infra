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


		public BinaryData Data { get; set; }

		public Type? DataType { get; set; }


		/// <summary>
		/// Number of messages that still need to be processed
		/// </summary>
		public long? MessageCount { get; set; }
		
		
		public T GetData<T>() where T : class {
			if (Data == null) return default;
			return Data.ToObjectFromJson<T>(JsonOptions);
		}

		public object? GetData() {
			if (DataType == null) return null;
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
			ServiceBusCommand msg = new ServiceBusCommand();
			msg.Name = arg.Message.Subject;
			msg.Target = arg.Message.ApplicationProperties.ContainsKey("Target") ? arg.Message.ApplicationProperties["Target"] as string : null;
			msg.Source = arg.Message.ApplicationProperties.ContainsKey("Source") ? arg.Message.ApplicationProperties["Source"] as string : null;

			if (arg.Message.EnqueuedTime != default) {
				msg.Timestamp = arg.Message.EnqueuedTime;
			} else if (arg.Message.ApplicationProperties.ContainsKey("Timestamp")) {
				string ts = (string)arg.Message.ApplicationProperties["Timestamp"];
				long epoch = 0;
				if (long.TryParse(ts, out epoch)) {
					msg.Timestamp = DateTimeOffset.FromUnixTimeSeconds(epoch);
				}
			}

			if (arg.Message.ApplicationProperties.ContainsKey("DataType")) {
				string[] dataType = ((string)arg.Message.ApplicationProperties["DataType"]).Split(',');
				msg.DataType = Type.GetType($"{dataType[0]}, {dataType[1]}", false);  //only use class and assembly name, ignore version
			}

			msg.Data = arg.Message.Body;

			return msg;
		}
	}
}
