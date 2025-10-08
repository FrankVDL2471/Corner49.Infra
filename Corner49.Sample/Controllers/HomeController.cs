using Corner49.Infra.Jobs;
using Corner49.Sample.Messages;
using Corner49.Sample.Models;
using Corner49.Sample.Repos;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Corner49.Sample.Controllers {
	public class HomeController : Controller {
		private readonly ILogger<HomeController> _logger;
		private readonly IDataMessageService _dataMessageService;
		private readonly IDataRepo _dataRepo;
		private readonly IJobManager _jobManager;

		public HomeController(ILogger<HomeController> logger, IDataMessageService dataMessageService, IDataRepo dataRepo, IJobManager jobManager) {
			_logger = logger;
			_dataMessageService = dataMessageService;
			_dataRepo = dataRepo;
			_jobManager = jobManager;	
		}

		public async Task<IActionResult> Index() {
			_logger.LogInformation("Load HomePage");


			foreach(var job in _jobManager.GetJobs()) {
				Console.WriteLine($"{job.Id}");
			}


			var qry = await _dataRepo.Query(q => q.Where(c => c.EnumDropdown == TestEnum.Enum1));
			return View(new DataModel());
		}



		[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
		public IActionResult Error() {
			return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
		}
	}
}
