using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;

namespace Corner49.Infra.Logging {


	/// <summary>
	/// A custom telemetry initializer for Application Insights that enhances request telemetry with additional properties.
	/// This initializer:
	/// - Overrides the "Success" status for specific HTTP response codes to avoid marking them as errors in Application Insights.
	/// - Captures and logs specific HTTP request headers.
	/// - Records the remote IP address of the client.
	/// </summary>
	public class AppInsightsTelemetryInitializer : ITelemetryInitializer {

		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly List<int> _statusCodesToOverride = new()
		{
						422, // no availability
            401, // unauthorized
            409, // no user
            404 // resource not found
        };

		public AppInsightsTelemetryInitializer(IHttpContextAccessor httpContextAccessor) {

			this._httpContextAccessor = httpContextAccessor;
		}

		public void Initialize(ITelemetry telemetry) {
			// set custom role name here
			telemetry.Context.Cloud.RoleName = InfraBuilder.Instance.Name;

			if (telemetry is not RequestTelemetry requestTelemetry || _httpContextAccessor?.HttpContext == null) return;


			bool parsed = int.TryParse(requestTelemetry.ResponseCode, out var responseCode);
			if (!parsed) return;

			// By overriding the status codes, certain response codes won't be logged as errors.
			// We try to avoid logging some as errors, to prevent clogging the logs.
			if (_statusCodesToOverride.Contains(responseCode)) {
				requestTelemetry.Success = true;
				requestTelemetry.Properties.Add("OverrideStatusCode", requestTelemetry.ResponseCode);
			}


			var context = _httpContextAccessor.HttpContext;
			requestTelemetry.Properties["Remote Ip Address"] = context.Request.HttpContext.Connection.RemoteIpAddress?.ToString();
			foreach (var header in _requestHeadersToRecord) {
				if (context.Request.Headers.TryGetValue(header, out var value)) {
					requestTelemetry.Properties["Request Header: " + header] = value.ToString();
				}
			}

		}



		private static List<string> _requestHeadersToRecord = new List<string> { "ApiKey" };

		public static void TrackHeader(string nm) {
			if (_requestHeadersToRecord.Contains(nm)) return;
			_requestHeadersToRecord.Add(nm);
		}

		/// <summary>
		/// 
		/// </summary>
		
	}
}
