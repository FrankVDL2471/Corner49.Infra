using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Corner49.Infra.Health {



	public class HealthService : IHealthCheck {

		public HealthService() {
		}

		private static readonly List<IHealthStatus> _triggers = new List<IHealthStatus>();

		public static void AddCheck(IHealthStatus check) {
			_triggers.Add(check);
		}


		public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
			if (!_triggers.Any()) return Task.FromResult(HealthCheckResult.Healthy());

			foreach (var trg in _triggers) {
				if (!trg.IsRunning) return Task.FromResult(HealthCheckResult.Unhealthy($"Trigger {trg.Name} is not running"));
			}

			return Task.FromResult(HealthCheckResult.Healthy());
		}
	}
}
