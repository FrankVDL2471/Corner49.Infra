using Corner49.Infra.Tools;
using System.Text.Json;

namespace Corner49.LogViewer.Models {
	public class HttpLogMessage {
		public DateTimeOffset Time { get; set; }
		public DateTime EventTime { get; set; }
		public string ResourceId { get; set; }
		public string Properties { get; set; }
		public string Category { get; set; }
		public string EventStampType { get; set; }
		public string EventPrimaryStampName { get; set; }
		public string EventStampName { get; set; }
		public string Host { get; set; }
		public string EventIpAddress { get; set; }


		public LogMessage Create() {
			var props = JsonSerializer.Deserialize<HttpProperties>(this.Properties);

			return new LogMessage {
				Time = Time,
				Category = Category,
				Level = props.Result == "Success" ? LogLevel.Info : LogLevel.Fail,
				Message = $"{props.CsMethod} {props.CsHost}{props.CsUriStem}?{props.CsUriQuery} -> {props.ScStatus} {props.Result}"
			};
		}

	}

	public class HttpProperties {
		public string CsHost { get; set; }
		public string CIp { get; set; }
		public string SPort { get; set; }
		public string CsUriStem { get; set; }
		public string CsUriQuery { get; set; }
		public string CsMethod { get; set; }
		public int TimeTaken { get; set; }
		public string ScStatus { get; set; }
		public string Result { get; set; }
		public string CsBytes { get; set; }
		public string ScBytes { get; set; }
		public string UserAgent { get; set; }
		public string Cookie { get; set; }
		public string CsUsername { get; set; }
		public string Referer { get; set; }
		public string ComputerName { get; set; }
		public string Protocol { get; set; }
	}





}
