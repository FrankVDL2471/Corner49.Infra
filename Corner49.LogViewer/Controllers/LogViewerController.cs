using Corner49.Infra.Storage;
using Corner49.LogViewer.Models;
using Corner49.LogViewer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Corner49.LogViewer.Controllers {


	public class LogViewerController : Controller {

		private readonly LogReaderService _reader;

		public LogViewerController(IConfiguration config) {
			string connectString = config.GetSection($"Storage:Logs:ConnectString")?.Value;
			_reader = new LogReaderService(connectString);
		}
		public async Task<IActionResult> Index([FromQuery] LogFilter filter) {
			
			LogViewerModel model = new LogViewerModel();
			model.App = filter.App;
			model.Date = filter.Date ?? DateTime.Today;
			model.Hour = filter.Hour;
			model.Level = filter.Level;
			model.Sorting = filter.Sorting;

			model.Apps = new List<KeyValuePair<object, string>>();
			await foreach(var app in _reader.GetApps()) {
				model.Apps.Add(new KeyValuePair<object, string>(app, app));
			}




			if (model.App != null) {
				model.Messages = await _reader.GetLogs(model);
			} else {
				model.Messages = new List<LogMessage>();
			}



				return View(model);
		}

		[HttpPost]
		public IActionResult Filter(LogViewerModel data, IFormCollection collection) {
			LogFilter filter = new LogFilter();
			filter.Date = data.Date;
			filter.Hour = data.Hour;
			filter.App = data.App;
			filter.Level = data.Level;	
			filter.Sorting = data.Sorting;
			
			return RedirectToAction("Index", filter);
		}


	}
}
