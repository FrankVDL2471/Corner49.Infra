using Corner49.Infra.Tools;
using System.Text.Json;

namespace Corner49.LogViewer.Models {
	public class FunctionLogMessage {
		public string OperationName { get; set; }
		public string Location { get; set; }
		public string ResourceId { get; set; }
		public string Category { get; set; }
		public string Properties { get; set; }
		public string Level { get; set; }
		public string Time { get; set; }
		public string EventStampType { get; set; }
		public string EventPrimaryStampName { get; set; }
		public string EventStampName { get; set; }
		public string Host { get; set; }
		public string EventIpAddress { get; set; }


		public LogMessage? Create() {
			if (this.Properties?.StartsWith("{") == true) {
				try {
					var data = JsonSerializer.Deserialize<FunctionLogData>(this.Properties.Replace("'", "\""), JsonHelper.Options);

					var msg = new LogMessage {
						Time = DateTime.ParseExact(this.Time, "dd/MM/yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture),	
						Category = data.Category,
						Message = data.Message
					};
					if (data.Level == "Information") msg.Level = Corner49.LogViewer.Models.LogLevel.Info;
					if (data.Level == "Error") msg.Level = Corner49.LogViewer.Models.LogLevel.Fail;
					if (data.Level == "Warning") msg.Level = Corner49.LogViewer.Models.LogLevel.Warn;

					return msg;
				} catch (Exception err) {
					return null;
				}

			} else if (!string.IsNullOrEmpty(this.Properties)) {

				var msg = new LogMessage {
					Category = this.Category,
					Message = this.Properties,
					Time = DateTime.ParseExact(this.Time, "dd/MM/yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture),
				};
				if (this.Level == "Informational") msg.Level = LogLevel.Info;
				if (this.Level == "Error") msg.Level = LogLevel.Fail;
				if (this.Level == "Warning") msg.Level = LogLevel.Warn;

				return msg;

			}

			return null;

		}


	}


	public class FunctionLogData {
		public string AppName { get; set; }
		public string RoleInstance { get; set; }
		public string Message { get; set; }
		public string Category { get; set; }
		public string HostVersion { get; set; }
		public string FunctionInvocationId { get; set; }
		public string FunctionName { get; set; }
		public string HostInstanceId { get; set; }
		public string Level { get; set; }
		public int LevelId { get; set; }
		public int ProcessId { get; set; }
		public int EventId { get; set; }
		public string EventName { get; set; }
	}


}
