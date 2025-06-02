using Corner49.Infra;
using Corner49.Sample.Handlers;
using Corner49.Sample.Messages;
using Corner49.Sample.Repos;
using Corner49.LogViewer;
using Microsoft.CodeAnalysis.CSharp.Syntax;

var infra = WebApplication.CreateBuilder(args)
	.UseInfra("Sample")
	.WithViewControllers(null, mvc => mvc.AddLogViewer() )
	.WithLogging(c => {
		c.WriteToConsoleAsJson = true;
		c.FilterCategoryPrefix = new string[] {
			"Corner49"
		};
	})
	.WithAuth0()
	.AddServiceBus()
	.AddDocumentDB(bld => {
		bld.Configure = (cfg) => {
			cfg.DatabaseName = "test";
		};

		bld.AddRepo<IDataRepo, DataRepo>();
	});



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
await infra.BuildAndRun((app) => {
	return Task.CompletedTask;
});
