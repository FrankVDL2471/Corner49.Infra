using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Corner49.Infra.Tools;
using Corner49.LogViewer.Models;
using Microsoft.VisualBasic;
using System.Collections.Concurrent;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text.Json;

namespace Corner49.LogViewer.Services {



	public class LogReaderService {

		private readonly BlobServiceClient _blobService;
		private List<BlobContainerClient> _containers;

		public LogReaderService(string blobConnectString) {
			_blobService = new BlobServiceClient(blobConnectString);

			_containers = new List<BlobContainerClient>();
			foreach (var container in _blobService.GetBlobContainers()) {
				_containers.Add(new BlobContainerClient(blobConnectString, container.Name));
			}
		}

		public IEnumerable<string> Containers => _containers.Select(c => c.Name);



		private bool _initDone = false;
		private Dictionary<string, List<string>> _apps = new Dictionary<string, List<string>>();

		private async Task Init() {
			if (_initDone) return;
			_initDone = true;

			await foreach (var blob in GetBlobs(null)) {
				string[] flds = blob.Name.Split('/');
				if (flds.Length > 8) {
					string app = flds[8];

					string src = string.Join('/', flds.Take(9));

					if (!_apps.ContainsKey(app)) _apps.Add(app, new List<string>());
					if (_apps[app].Contains(src)) continue;
					_apps[app].Add(src);
				}
			}
		}

		public async IAsyncEnumerable<string> GetApps() {
			await Init();
			foreach (var key in _apps.Keys) {
				yield return key;
			}
		}

		public async Task<IEnumerable<LogMessage>> GetLogs(LogFilter filter) {
			await Init();

			ConcurrentBag<LogMessage> logs = new ConcurrentBag<LogMessage>();

			
			



			if ((filter.App != null) && (_apps.ContainsKey(filter.App))) {
				foreach (var root in _apps[filter.App]) {
					string path = root;
					if (filter.Date != null) {
						path += $"/y={filter.Date.Value.Year.ToString("0000")}/m={filter.Date.Value.Month.ToString("00")}/d={filter.Date.Value.Day.ToString("00")}";
						if (filter.Hour != null) {
							int hour = DateTime.Today.AddHours(filter.Hour.Value).ToUniversalTime().Hour;
							path += $"/h={hour}";
						}
					}

					await Parallel.ForEachAsync(this.GetBlobs(path), async (itm, ct) => {
						await this.Read(filter.App, itm.Name, logs);
					});
				}
			}
			if (filter.Sorting == "DESC") {
				return logs.Where(c => c != null).OrderByDescending(c => c.Time);
			} else {
				return logs.Where(c => c != null).OrderBy(c => c.Time);
			}
		}

		public async Task Read(string appName, string path, ConcurrentBag<LogMessage> logs,  CancellationToken cancellationToken = default) {
			await Parallel.ForEachAsync(_containers, async (container, ct) => {
				var client = container.GetBlobClient(path);
				if (await client.ExistsAsync()) {

					using (var stream = await client.OpenReadAsync()) {
						using (StreamReader reader = new StreamReader(stream)) {
							while (!reader.EndOfStream) {
								if (cancellationToken.IsCancellationRequested) break;
								string? line = await reader.ReadLineAsync();
								if (line == null) continue;

								if (container.Name == "insights-logs-appserviceapplogs") {
									var msg = JsonSerializer.Deserialize<DiagnosticLogMessage>(line, JsonHelper.Options)?.Create();
									if (msg != null) logs.Add(msg);
								} else if (container.Name == "insights-logs-appservicehttplogs") {
									var msg = JsonSerializer.Deserialize<HttpLogMessage>(line, JsonHelper.Options)?.Create();
									if (msg != null) logs.Add(msg);
								} else if (container.Name == "insights-logs-appserviceconsolelogs") {
									var msg = JsonSerializer.Deserialize<ConsoleLogMessage>(line, JsonHelper.Options)?.Create();
									if (msg != null) logs.Add(msg);
								}


							}
						}
					}
				}
			});
		}




		private async IAsyncEnumerable<BlobItem> GetBlobs(string? prefix = null) {
			foreach (var container in _containers) {
				await foreach (var tree in container.GetBlobsByHierarchyAsync(prefix: prefix)) {
					yield return tree.Blob;
				}
			}
		}




	}
}
