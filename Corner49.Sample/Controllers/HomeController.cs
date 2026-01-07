using Corner49.Infra.Jobs;
using Corner49.Infra.ServiceBus;
using Corner49.Infra.Storage;
using Corner49.Sample.Messages;
using Corner49.Sample.Models;
using Corner49.Sample.Repos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Build.Framework;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Corner49.Sample.Controllers {
	public class HomeController : Controller {
		private readonly ILogger<HomeController> _logger;
		
		private readonly IDataRepo _dataRepo;
		private readonly IJobManager _jobManager;
		private readonly IBlobService _blob;
		private readonly IServiceBusService _serviceBus;

		public HomeController(ILogger<HomeController> logger, IConfiguration config, IDataRepo dataRepo, IJobManager jobManager, IServiceBusService serviceBus) {
			_logger = logger;
			_dataRepo = dataRepo;
			_jobManager = jobManager;
			_serviceBus = serviceBus;	

			_blob = new BlobService("Public", config);
		}

		public async Task<IActionResult> Index() {
			_logger.LogInformation("Load HomePage");


			foreach(var job in _jobManager.GetJobs()) {
				Console.WriteLine($"{job.Id}");
			}


			var data = new MemoryStream();
			//var fl = await _blob.GetFile("test", "test.xml", data);
			//var img = await _blob.GetFile("ottogusto", "cat_001.png", data);

			//var qry = await _dataRepo.Query(q => q.Where(c => c.EnumDropdown == TestEnum.Enum1));
			return View(new DataModel());
		}


		public async Task<IActionResult> DLQ() {
			
			await _serviceBus.ResubmitDeadletterQueue(new ServiceBusOptions(string.Empty) { Name = "clean_parts", Kind = ServiceBusKind.Queue });
			return RedirectToAction("Index");
		}




			[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
		public IActionResult Error() {
			return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
		}
	}
}
