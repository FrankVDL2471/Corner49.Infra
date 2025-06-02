namespace Corner49.LogViewer.Models {
	public class LogMessage {
		public DateTimeOffset? Time { get; set; }
		public LogLevel? Level { get; set; }
		public string? Message { get; set; }
		public string? Category { get; set; }
	}

	public enum LogLevel { 
		Info,
		Warn, 
		Fail,
		Crsh,
	}


}
