
using Auth0.AspNetCore.Authentication.BackchannelLogout;
using Corner49.Infra;
using Corner49.Infra.Messages;
using Corner49.Infra.Storage;
using Corner49.SampleAPI.Handlers;
using System.Reflection.Metadata.Ecma335;

namespace Corner49.SampleAPI {
	public class Program {
		public static async Task Main(string[] args) {
			var infra = WebApplication.CreateBuilder(args)
				.UseInfra("Corner49")
				.WithLogging()
				.WithApiControllers()
				.WithCors((host) => true)
				.WithHealthCheck();

			infra.WithAuth0((opt) => {
				opt.Domain = infra.Configuration["Auth:Domain"];
				opt.Audience = infra.Configuration["Auth:Audience"];
				opt.ClientId = infra.Configuration["Auth:ClientId"];
				opt.ClientSecret = infra.Configuration["Auth:ClientSecret"];
			});


			//infra.AddJobs((bld) => { },
			// (cfg) => {
			//	 cfg.UseSqlServer = true;
			//	 cfg.ConnectString = infra.Configuration["ConnectionStrings:ConnectionString"];
			//	 cfg.DbName = "jobs-dev-test";
			// });

			infra = infra.AddServiceBus(cfg => {
				//cfg.DeveloperMode = true;
//				cfg.IsBasicTier = true;
			});

//			infra.AddMessageHandler<TestMessage, TestMessageHandler>();


			//infra.AddServiceBusHandler<Handlers.MessageHandler>(cfg => {
			//	cfg.Name = "BCentral";
			//	cfg.Kind = Corner49.Infra.ServiceBus.ServiceBusKind.Queue;
			//	cfg.DuplicateDetectionWindow = TimeSpan.FromMinutes(1);
			//	cfg.MaxConcurrentCalls = 5;
			//	cfg.TrackMessageCount = true;
			//});



			//infra = infra.AddServiceBusHandler<Handlers.MessageHandler>(cfg => {
			//	cfg.Name = "topictest";
			//	cfg.Kind = Infra.ServiceBus.ServiceBusKind.Topic;
			//});

			//Custom services

			await infra.BuildAndRun();
		}
	}
}
