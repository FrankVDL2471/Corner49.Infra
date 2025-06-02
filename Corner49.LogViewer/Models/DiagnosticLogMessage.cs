namespace Corner49.LogViewer.Models {
	public class DiagnosticLogMessage {
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


		public LogMessage Create() {
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
	}


}
