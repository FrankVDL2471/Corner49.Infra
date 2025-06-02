using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Text.Json;

namespace Corner49.Infra.Logging {
	public interface ITelemetryService {
		void TrackMetric(MetricTelemetry metric);
		void TrackTrace(string msg);
		void TrackException(Exception err);

		DependencyTracker TrackDependency(string type, string name);

		IDisposable StartOperation(RequestTelemetry metric);

	}

	public class TelemetryService : ITelemetryService {
		private readonly TelemetryClient? _telemetry;
		public TelemetryService(IConfiguration? config) {
			if (config != null) {
				var connectstring = config["APPLICATIONINSIGHTS_CONNECTION_STRING"] ?? config["AppInsights:ConnectionString"];
				if (!string.IsNullOrEmpty(connectstring)) {
					_telemetry = new TelemetryClient(new Microsoft.ApplicationInsights.Extensibility.TelemetryConfiguration {
						ConnectionString = connectstring
					});
				} else {
					var instrumationKey = config["ApplicationInsights:InstrumentationKey"];
					if (!string.IsNullOrEmpty(instrumationKey)) {
						_telemetry = new TelemetryClient(new Microsoft.ApplicationInsights.Extensibility.TelemetryConfiguration {
							InstrumentationKey = instrumationKey
						});

					}
				}
			}
		}


		public void TrackMetric(MetricTelemetry metric) {
			if (_telemetry != null) _telemetry.TrackMetric(metric);
		}
		public void TrackTrace(string msg) {
			if (_telemetry != null) _telemetry.TrackTrace(msg);
		}
		public void TrackException(Exception err) {
			if (_telemetry != null) _telemetry.TrackException(err);
		}


		public DependencyTracker TrackDependency(string type, string name) {
			return new DependencyTracker(_telemetry, type, name);
		}
		public IDisposable StartOperation(RequestTelemetry metric) {
			if (_telemetry != null) return _telemetry.StartOperation(metric);
			return new EmptyOperation();
		}
	}


	public class DependencyTracker : IDisposable {
		private readonly TelemetryClient? _client;
		private readonly string _type;
		private readonly string _name;

		private DateTimeOffset _start;
		private Stopwatch _sw;

		private bool _success = true;
		public DependencyTracker(TelemetryClient? client, string type, string name) {
			_client = client;
			_type = type;
			_name = name;

			_start = DateTimeOffset.UtcNow;
			_sw = Stopwatch.StartNew();
		}

		public void SetFailed(Exception? err = null, object? data = null) {
			if (!_sw.IsRunning) return;
			_sw.Stop();

			if (err != null) {
				if (_client != null) {
					string body = data == null ? string.Empty : JsonSerializer.Serialize(data);
					_client.TrackDependency(_type, _name, body, _start, _sw.Elapsed, false);
					_client.TrackException(err);
				}
			}
		}


		public void Finish(bool success, object? data = null) {
			if (!_sw.IsRunning) return;
			_sw.Stop();

			if (_client != null) {
				string body = string.Empty;
				if (data is string txt) {
					body = txt;
				} else if (data != null) {
					body = JsonSerializer.Serialize(data);
				}

				_client.TrackDependency(_type, _name, body, _start, _sw.Elapsed, success);
			}
		}

		public void LogStep(string msg) {
			if (!_sw.IsRunning) return;
			if (_client != null) {
				_client.TrackDependency(_type, _name, msg, _start, _sw.Elapsed, true);
			}
		}

		public void Dispose() {
			this.Finish(_success, null);
		}
	}

	public class EmptyOperation : IDisposable {
		public void Dispose() {
		}
	}

}
