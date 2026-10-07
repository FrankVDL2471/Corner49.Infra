using Corner49.Infra;
using Corner49.LogViewer;
using Corner49.Sample.Messages;
using Corner49.Sample.Repos;

public partial class Program {
	private static async Task Main(string[] args) {
		var infra = WebApplication.CreateBuilder(args)
	.UseInfra("Sample", "Development")
	.WithViewControllers(null, mvc => mvc.AddLogViewer())
	.WithLogging(c => {
		c.WriteToConsoleAsJson = true;
		c.FilterCategoryPrefix = new string[] {
			"Corner49"
		};
	})
	.WithAuth0();



		infra.AddDocumentDB(bld => {
			bld.Configure = (cfg) => {
				cfg.DatabaseName = "dev-ottogusto";
			};

			bld.AddRepo<IDataRepo, DataRepo>();
		});

		//infra.AddJobs();

		//infra.AddJobs((bld) => {
		//	bld.AddCronJob<TestJob>((cron) => cron.EveryMinute(5));
		//}
		//					, cfg => {
		//						cfg.UseLocalQueue = true;
		//						cfg.DisableAutomaticRestart = true;
		//						cfg.QueueName = "test";
		//						cfg.UseSqlServer = true;
		//						cfg.ConnectString = infra.Configuration["ConnectionStrings:ConnectionString"];
		//						cfg.DbName = $"jobs-dev";

		//					});

		infra.Services.AddSingleton<IDataMessageService, DataMessageService>();



		//infra.AddServiceBusHandler<BusHandler>(cfg => {
		//	cfg.Name = "samplequeue";
		//	cfg.Kind = Corner49.Infra.ServiceBus.ServiceBusKind.Queue;
		//#if DEBUG
		//	cfg.MaxConcurrentCalls = 1;
		//#else
		//				cfg.MaxConcurrentCalls = 50;
		//#endif
		//});



		//Custom services


		//Build and run
		await infra.BuildAndRun();
	}
}