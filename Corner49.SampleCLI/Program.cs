using Corner49.CLI;
using Corner49.CosmosDB;
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



			infra.AddJob<Jobs.HelloWorldJob>("Hello", "Print hello world in the console");
			infra.AddJob<Jobs.ListPartsJob>("Parts", "List all parts in the database");




			await infra.BuildAndRun(args);

		}
	}
}
