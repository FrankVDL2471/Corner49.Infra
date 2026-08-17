using Microsoft.Azure.Cosmos;
using OpenTelemetry.Trace;

namespace Corner49.Infra.DB {

	/// <summary>
	/// Snapshot of client-observed Cosmos DB throughput usage for a single container, used to decide
	/// whether a processing job should pause before it starts getting 429-throttled.
	/// </summary>
	/// <remarks>
	/// <see cref="RequestUnitsPerSecond"/> and <see cref="PressureRatio"/> only reflect RU usage made
	/// through the owning <see cref="DocumentRepo{T}"/> instance (i.e. this process) - they are blind to
	/// other app instances or other containers sharing the same database-level throughput.
	/// <see cref="Throttled429Count"/> is a useful cross-check for that blind spot: a 429 means the
	/// container is over budget account-wide, regardless of who caused it.
	/// </remarks>
	public class ThroughputStats {

		/// <summary>Rolling average RU/s consumed by this process against this container over <see cref="Window"/>.</summary>
		public double RequestUnitsPerSecond { get; set; }

		/// <summary>
		/// Provisioned RU/s for the container (or its database, if throughput is database-shared).
		/// Null if unknown - e.g. a serverless account (no fixed RU/s) or the throughput read failed.
		/// </summary>
		public int? ProvisionedRequestUnits { get; set; }

		/// <summary>
		/// <see cref="RequestUnitsPerSecond"/> divided by <see cref="ProvisionedRequestUnits"/>. Null if
		/// <see cref="ProvisionedRequestUnits"/> is unknown.
		/// </summary>
		public double? PressureRatio { get; set; }

		/// <summary>Count of 429 TooManyRequests responses observed (by this process) within <see cref="Window"/>.</summary>
		public int Throttled429Count { get; set; }

		/// <summary>Duration of the rolling window these stats were computed over.</summary>
		public TimeSpan Window { get; set; }


		public bool IsUnderPressure(double threshold = 0.8) {
			return (this.PressureRatio.HasValue && this.PressureRatio.Value >= threshold) || this.Throttled429Count > 0;
		}

	}

	/// <summary>
	/// Tracks client-observed RU consumption and 429s for a single Cosmos container over a rolling,
	/// per-second bucketed window, and caches the container's actual provisioned RU/s (refreshed
	/// periodically since it rarely changes). One instance is owned per <see cref="DocumentRepo{T}"/>.
	/// </summary>
	internal class ThroughputTracker {

		private readonly int _windowSeconds;
		private readonly double[] _ruBuckets;
		private readonly int[] _throttleBuckets;
		private readonly long[] _bucketSecond;
		private readonly object _lock = new object();

		private readonly Func<Task<int?>> _readProvisionedThroughput;
		private readonly TimeSpan _provisionedRefreshInterval;
		private int? _provisionedRequestUnits;
		private DateTime _provisionedReadAtUtc = DateTime.MinValue;
		private Task<int?>? _refreshInFlight;

		public ThroughputTracker(Func<Task<int?>> readProvisionedThroughput, int windowSeconds = 10, TimeSpan? provisionedRefreshInterval = null) {
			_readProvisionedThroughput = readProvisionedThroughput;
			_windowSeconds = Math.Max(1, windowSeconds);
			_ruBuckets = new double[_windowSeconds];
			_throttleBuckets = new int[_windowSeconds];
			_bucketSecond = new long[_windowSeconds];
			_provisionedRefreshInterval = provisionedRefreshInterval ?? TimeSpan.FromMinutes(5);
		}

		/// <summary>Records the RU charge and/or a 429 observed on a single Cosmos call.</summary>
		public void Record(double? requestCharge, bool throttled) {
			long nowSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			int idx = (int)(nowSec % _windowSeconds);
			lock (_lock) {
				if (_bucketSecond[idx] != nowSec) {
					_ruBuckets[idx] = 0;
					_throttleBuckets[idx] = 0;
					_bucketSecond[idx] = nowSec;
				}
				if (requestCharge.HasValue) _ruBuckets[idx] += requestCharge.Value;
				if (throttled) _throttleBuckets[idx]++;
			}
		}

		/// <summary>Computes the current rolling-window stats, refreshing the cached provisioned RU/s if stale.</summary>
		public async Task<ThroughputStats> GetSnapshot() {
			await EnsureProvisionedThroughputFresh();

			long nowSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			double ruSum = 0;
			int throttleSum = 0;
			lock (_lock) {
				for (int i = 0; i < _windowSeconds; i++) {
					if (nowSec - _bucketSecond[i] < _windowSeconds) {
						ruSum += _ruBuckets[i];
						throttleSum += _throttleBuckets[i];
					}
				}
			}

			int? provisioned = _provisionedRequestUnits;
			double rups = ruSum / _windowSeconds;

			return new ThroughputStats {
				RequestUnitsPerSecond = rups,
				ProvisionedRequestUnits = provisioned,
				PressureRatio = provisioned.HasValue && provisioned.Value > 0 ? rups / provisioned.Value : (double?)null,
				Throttled429Count = throttleSum,
				Window = TimeSpan.FromSeconds(_windowSeconds)
			};
		}

		private async Task EnsureProvisionedThroughputFresh() {
			if (_provisionedReadAtUtc != DateTime.MinValue && DateTime.UtcNow - _provisionedReadAtUtc < _provisionedRefreshInterval) return;

			Task<int?> refresh;
			lock (_lock) {
				_refreshInFlight ??= _readProvisionedThroughput();
				refresh = _refreshInFlight;
			}

			try {
				_provisionedRequestUnits = await refresh;
			} catch {
				// keep the previously cached value (possibly null) on failure
			} finally {
				lock (_lock) {
					if (_refreshInFlight == refresh) {
						_refreshInFlight = null;
						_provisionedReadAtUtc = DateTime.UtcNow;
					}
				}
			}
		}

	}
}
