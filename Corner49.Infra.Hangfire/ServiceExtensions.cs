using Corner49.Infra.Jobs;

namespace Corner49.Infra {
	public static class ServiceExtensions {


		public static IInfraBuilder AddJobs(this IInfraBuilder infra, Action<JobBuilder>? builder, Action<JobConfig>? config = null) {
			infra.AddExtension(new HangfireExtension(builder, config));
			return infra;
		}


	}
}
