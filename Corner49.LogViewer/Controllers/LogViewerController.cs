using Corner49.Infra.Storage;
using Corner49.LogViewer.Models;
using Corner49.LogViewer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Security.Cryptography.Pkcs;
using System.Text;
using System.Threading.Tasks;

namespace Corner49.LogViewer.Controllers {


	public class LogViewerController : Controller {

		private readonly LogReaderService _reader;

		public LogViewerController(IConfiguration config) {
			string connectString = config.GetSection($"Storage:Logs:ConnectString")?.Value;
			_reader = new LogReaderService(connectString);
		}
		public async Task<IActionResult> Index([FromQuery] LogFilter filter, string? action = null) {

			LogViewerModel model = await this.GetModel(filter);


			return View(model);
		}

		[HttpPost]
		public async Task<IActionResult> Filter(LogViewerModel data, IFormCollection collection) {
			LogFilter filter = new LogFilter();
			filter.Date = data.Date;
			filter.Hour = data.Hour;
			filter.App = data.App;
			filter.Level = data.Level;
			filter.Sorting = data.Sorting;
			filter.Category = data.Category;

			return RedirectToAction("Index", filter);
		}


		public async Task<IActionResult> Download([FromQuery] LogFilter filter) {
			var model = await this.GetModel(filter);

			string name = $"logs_{filter.Category}_{filter.Date.Value.ToString("yyyyMMdd")}.txt";

			MemoryStream mem = new MemoryStream();
			StreamWriter writer = new StreamWriter(mem, System.Text.Encoding.UTF8);

			foreach (var msg in model.Messages) {
				await writer.WriteLineAsync($"[{msg.Time.Value.ToString("dd/MM/yyyy HH:mm:ss.fffff")}|{msg.Level}|{msg.Category}] {msg.Message}");
			}
			await writer.FlushAsync();

			mem.Position = 0;
			return File(mem, "text/plain", name);
		}


		private async Task<LogViewerModel> GetModel(LogFilter filter) {
			LogViewerModel model = new LogViewerModel();
			model.App = filter.App;
			model.Date = filter.Date ?? DateTime.Today;
			model.Hour = filter.Hour;
			model.Level = filter.Level;
			model.Sorting = filter.Sorting;
			model.Category = filter.Category;

			model.Apps = new List<KeyValuePair<object, string>>();
			await foreach (var app in _reader.GetApps()) {
				model.Apps.Add(new KeyValuePair<object, string>(app, app));
			}




			if (model.App != null) {
				model.Messages = await _reader.GetLogs(model);
			} else {
				model.Messages = new List<LogMessage>();
			}

			return model;

		}

	}
}
