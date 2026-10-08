using Corner49.Infra.Jobs;
using Microsoft.Extensions.Configuration;

namespace Corner49.Infra {
	public class HangfireExtension : InfraExtension {

		private Action<JobBuilder>? _builder;
		private Action<JobConfig>? _config;	

		public HangfireExtension(Action<JobBuilder>? builder, Action<JobConfig>? config = null) {
			_builder = builder;
			_config = config;	
		}


		private JobBuilder? _jobs = null;

		public override Task Build(IInfraBuilder infra, IConfiguration config) {
			JobConfig cfg = new JobConfig();
			cfg.EnableDashboard = true;
			if (_config != null) _config(cfg);

			_jobs = new JobBuilder(infra.Services, cfg);
			if (_builder != null) {
				_builder(_jobs);
			}

			return Task.CompletedTask;
		}


		public override Task Start(IServiceProvider serviceProvider) {

			//if (_jobs != null) _jobs.UseDashboard(app, _appName);

			return base.Start(serviceProvider);
		}
	}
}
