namespace Corner49.LogViewer.Models {
	public class LogViewerModel : LogFilter {

		public List<KeyValuePair<object, string>> Apps { get; set; }

		public IEnumerable<LogMessage> Messages { get; set; }

	}
}
