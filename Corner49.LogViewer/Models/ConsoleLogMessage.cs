using Corner49.Infra.Tools;
using System.Diagnostics;
using System.Text.Json;

namespace Corner49.LogViewer.Models {


	public class ConsoleLogMessage {
		public string? Level { get; set; }
		public string? ResourceId { get; set; }
		public DateTimeOffset? Time { get; set; }
		public string? ResultDescription { get; set; }
		public string? Category { get; set; }
		public string? EventStampType { get; set; }
		public string? EventPrimaryStampName { get; set; }
		public string? EventStampName { get; set; }
		public string? Host { get; set; }
		public string? EventIpAddress { get; set; }


		public LogMessage ? Create() {
			if (this.ResultDescription?.StartsWith("{") == true) {
				try {
					var data = JsonSerializer.Deserialize<ConsoleLogData>(this.ResultDescription);

					var msg = new LogMessage {
						Time = this.Time,
						Category = data.Category,
						Message = data.Message
					};
					if (data.LogLevel == "Information") msg.Level = Corner49.LogViewer.Models.LogLevel.Info;
					if (data.LogLevel == "Error") msg.Level = Corner49.LogViewer.Models.LogLevel.Fail;
					if (data.LogLevel == "Warning") msg.Level = Corner49.LogViewer.Models.LogLevel.Warn;

					return msg;
				} catch(Exception err) {
					return null;
				}

			} else if (!string.IsNullOrEmpty(this.ResultDescription)) {

				var msg = new LogMessage {
					Category = Category,
					Message = ResultDescription,
					Time = Time
				};
				if (this.Level == "Information") msg.Level = LogLevel.Info;
				if (this.Level == "Error") msg.Level = LogLevel.Fail;
				if (this.Level == "Warning") msg.Level = LogLevel.Warn;

				return msg;

			}

			return null;

		}
	}

	public class ConsoleLogData {
		public string Timestamp { get; set; }
		public int EventId { get; set; }
		public string LogLevel { get; set; }
		public string Category { get; set; }
		public string Message { get; set; }
		public ConsoleLogState State { get; set; }
		public ConsoleLogScope[] Scopes { get; set; }


	}

	public class ConsoleLogState {
		public string Message { get; set; }
		public string OriginalFormat { get; set; }
	}

	public class ConsoleLogScope {
		public string Message { get; set; }
		public string SpanId { get; set; }
		public string TraceId { get; set; }
		public string ParentId { get; set; }
		public string ConnectionId { get; set; }
		public string RequestId { get; set; }
		public string RequestPath { get; set; }
		public string ActionId { get; set; }
		public string ActionName { get; set; }
	}

}
