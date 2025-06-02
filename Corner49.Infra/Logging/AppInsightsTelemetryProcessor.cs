using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Corner49.Infra.Logging {
	public class AppInsightsTelemetryProcessor : ITelemetryProcessor {
		private readonly ITelemetryProcessor _next;
		
		public AppInsightsTelemetryProcessor(ITelemetryProcessor next) {
			_next = next;
		}

		public void Process(ITelemetry item) {

			if (item is RequestTelemetry request) {

				//Check ignored path
				if (_ignoreRequestPaths.Any(path => request.Url?.AbsolutePath.StartsWith(path, StringComparison.OrdinalIgnoreCase) ?? false)) {
					return;
				}

				bool success = request.Success == true;
				if (int.TryParse(request.ResponseCode, out var responseCode)) {
					if (responseCode < 300) success = true;
				}

				if ((LongRequestThreshold > 0) && (success)){
					if (request.Duration.TotalMilliseconds <= LongRequestThreshold)
						return;
				}
			}
						

			_next.Process(item);
		}


		private static List<string> _ignoreRequestPaths = new List<string> { "/health", "/swagger", "/robot", "/scalar" };

		public static void AddPath(string path) {
			string nm = path.ToLower();
			if (!nm.StartsWith("/")) nm = "/" + nm;
			if (_ignoreRequestPaths.Contains(nm)) return;
			_ignoreRequestPaths.Add(nm);
		}

		/// <summary>
		/// Only requests taking longer then this threshold (in ms) are tracked
		/// </summary>
		public static long LongRequestThreshold { get; set; } = 500;

	}
}
