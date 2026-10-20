using Corner49.Infra;
using Corner49.SampleCLI.Repos;

namespace Corner49.SampleCLI {
	internal class Program {
		static async Task Main(string[] args) {


			var infra = InfraBuilder.Create("Corner49.SampleCLI", args)
				.WithLogging(opt => {
					opt.TrackContent = false;
					opt.TrackDependencies = false;
					opt.WriteToConsoleAsJson = false;
				});


			infra.AddDocumentDB(bld => {
				bld.Configure = (cfg) => {
					cfg.DatabaseName = "dev-ottogusto";
				};

				bld.AddRepo<IDataRepo, DataRepo>();
			});
			infra.AddServiceBus(c => { });

			infra.Addservices();

			infra.AddJob<Jobs.HelloWorldJob>("Hello", "Print hello world in the console");
			infra.AddJob<Jobs.ListPartsJob>("Parts", "List all parts in the database");

			infra.AddJob<Jobs.GtmLookup>("GtmLookup", "Check the GTM lookup service bus queue");



			await infra.BuildAndRun(args);

		}
	}

	
}
