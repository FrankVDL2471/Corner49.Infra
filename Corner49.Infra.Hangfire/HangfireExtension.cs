using Corner49.Infra.Jobs;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Corner49.Infra {
	public class HangfireExtension : InfraExtension {

		private Action<JobBuilder>? _builder;
		private Action<JobConfig>? _config;	

		public HangfireExtension(Action<JobBuilder>? builder, Action<JobConfig>? config = null) {
			_builder = builder;
			_config = config;	
		}


		private JobBuilder? _jobs = null;
		private string? _appName = null;

		public override Task Build(IInfraBuilder infra, IConfiguration config) {
			_appName = infra.Name;

			JobConfig cfg = new JobConfig();
			cfg.EnableDashboard = true;
			if (_config != null) _config(cfg);

			_jobs = new JobBuilder(infra.Services, cfg);
			if (_builder != null) {
				_builder(_jobs);
			}

			return Task.CompletedTask;
		}


		public override Task Start(IServiceProvider serviceProvider, object? host = null) {
			
			if (host is IApplicationBuilder app) {
				if (_jobs != null) _jobs.UseDashboard(app, _appName ?? "App");
			}

			return base.Start(serviceProvider);
		}
	}
}
