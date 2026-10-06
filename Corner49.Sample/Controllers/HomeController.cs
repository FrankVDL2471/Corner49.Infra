using Corner49.Sample.Models;
using Corner49.Sample.Repos;
using Corner49.Sample.Services;
using Corner49.Storage;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace Corner49.Sample.Controllers {
	public class HomeController : Controller {
		private readonly ILogger<HomeController> _logger;

		private readonly IDataRepo _dataRepo;
		private readonly IBlobService _blob;

		public HomeController(ILogger<HomeController> logger, IConfiguration config, IDataRepo dataRepo) {
			_logger = logger;
			_dataRepo = dataRepo;

			_blob = new BlobService("Public", config);

		}




		public async Task<IActionResult> Index() {
			_logger.LogInformation("Load HomePage");



			var data = new MemoryStream();
			var blob = await _blob.GetBlob("ottogusto", "cat_001.png", data);

			//var fl = await _blob.GetFile("test", "test.xml", data);
			//var img = await _blob.GetFile("ottogusto", "cat_001.png", data);

			var model = await _dataRepo.GetItem("051225", "m3homqjo441hb4");
			//var qry = await _dataRepo.Query(q => q.Where(c => c.EnumDropdown == TestEnum.Enum1));
			return View(new DataModel());
		}




		public async Task<IActionResult> Test() 
			{


			await foreach (var data in _dataRepo.Export(null, "select * from c", 10)) {

				using (var reader = new StreamReader(data)) {
					var tst = reader.ReadToEnd();

					Console.WriteLine("-------");
					Console.WriteLine(tst);
					Console.WriteLine("-------");

				}
			}


			return RedirectToAction("Index");
		}


		public async Task<IActionResult> DummyApi() {
			var api = new DummyApi();
			var resp = await api.Test();


			return RedirectToAction("Index");
		}





		[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
		public IActionResult Error() {
			return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
		}
	}
}
