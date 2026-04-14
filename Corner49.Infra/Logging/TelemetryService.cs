using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
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


		public TelemetryService(TelemetryClient? telemetryClient) {
			_telemetry = telemetryClient;
			if (_telemetry != null) {
				_telemetry.Context.Cloud.RoleName = InfraBuilder.Instance.Name;
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
		private bool _disposed = false;

		public DependencyTracker(TelemetryClient? client, string type, string name) {
			_client = client;
			_type = type;
			_name = name;

			_start = DateTimeOffset.UtcNow;
			_sw = Stopwatch.StartNew();
		}

		public void SetFailed(Exception? err = null, object? data = null) {
			if (_disposed || !_sw.IsRunning) return;
			_sw.Stop();
			_disposed = true;

			if (err != null) {
				if (_client != null) {
					string body = data == null ? string.Empty : JsonSerializer.Serialize(data);
					_client.TrackDependency(_type, _name, body, _start, _sw.Elapsed, false);
					_client.TrackException(err);
				}
			}
		}


		public void Finish(bool success, object? data = null) {
			if (_disposed || !_sw.IsRunning) return;
			_sw.Stop();
			_disposed = true;

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


		public void Dispose() {
			this.Finish(_success, null);
		}
	}

	public class EmptyOperation : IDisposable {
		public void Dispose() {
		}
	}

	public class NoOpTelemetryService : ITelemetryService {
		public void TrackMetric(MetricTelemetry metric) { }
		public void TrackTrace(string msg) { }
		public void TrackException(Exception err) { }
		public DependencyTracker TrackDependency(string type, string name) {
			return new DependencyTracker(null, type, name);
		}
		public IDisposable StartOperation(RequestTelemetry metric) {
			return new EmptyOperation();
		}
	}

}
