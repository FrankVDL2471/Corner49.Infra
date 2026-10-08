using Hangfire.Annotations;
using Hangfire.Dashboard;

namespace Corner49.Infra.Jobs {
	public class JobAuth : IDashboardAuthorizationFilter {
		public bool Authorize([NotNull] DashboardContext context) {
			return true;
		}
	}
}
