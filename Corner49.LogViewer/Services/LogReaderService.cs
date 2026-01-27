using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Corner49.Infra.Tools;
using Corner49.LogViewer.Models;
using Microsoft.Azure.Cosmos.Linq;
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



		private static bool _initDone = false;
		private static Dictionary<string, List<string>> _apps = new Dictionary<string, List<string>>();

		private async Task Init() {
			if (_initDone) return;
			_initDone = true;

			await foreach (var blob in GetSubscriptions()) {
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

			DateTime dt = filter.Date ?? DateTime.Today;

			if (filter.Hour != null) {
				dt = dt.ToUniversalTime().AddHours(filter.Hour.Value);
			}



			if ((filter.App != null) && (_apps.ContainsKey(filter.App))) {
				foreach (var root in _apps[filter.App]) {
					string path = root;
					path += $"/y={dt.Year.ToString("0000")}/m={dt.Month.ToString("00")}/d={dt.Day.ToString("00")}";
					if (filter.Hour != null) {
						int hour = dt.Hour;
						path += $"/h={hour.ToString("00")}";
					}

					await Parallel.ForEachAsync(this.GetBlobs(path), async (itm, ct) => {
						await this.Read(filter.App, itm.Name, filter.Category, logs);
					});
				}
			}
			if (filter.Sorting == "DESC") {
				return logs.Where(c => c != null).OrderByDescending(c => c.Time);
			} else {
				return logs.Where(c => c != null).OrderBy(c => c.Time);
			}
		}

		public async Task Read(string appName, string path, string? category, ConcurrentBag<LogMessage> logs, CancellationToken cancellationToken = default) {
			await Parallel.ForEachAsync(_containers, async (container, ct) => {
				var client = container.GetBlobClient(path);
				if (await client.ExistsAsync()) {

					BlobOpenReadOptions options = new BlobOpenReadOptions(false);
					options.Conditions = new BlobRequestConditions();
					options.Conditions.IfMatch = ETag.All; //   (await client.GetPropertiesAsync()).Value.ETag;	


					try {

						using (var stream = await client.OpenReadAsync(options)) {
							using (StreamReader reader = new StreamReader(stream)) {
								while (!reader.EndOfStream) {
									if (cancellationToken.IsCancellationRequested) break;
									string? line = await reader.ReadLineAsync();
									if (line == null) continue;

									LogMessage? msg = null;
									if (container.Name == "insights-logs-appserviceapplogs") {
										msg = JsonSerializer.Deserialize<DiagnosticLogMessage>(line, JsonHelper.Options)?.Create();
									} else if (container.Name == "insights-logs-appservicehttplogs") {
										msg = JsonSerializer.Deserialize<HttpLogMessage>(line, JsonHelper.Options)?.Create();
									} else if (container.Name == "insights-logs-appserviceconsolelogs") {
										msg = JsonSerializer.Deserialize<ConsoleLogMessage>(line, JsonHelper.Options)?.Create();
									} else if (container.Name == "insights-logs-functionapplogs") {
										msg = JsonSerializer.Deserialize<FunctionLogMessage>(line, JsonHelper.Options)?.Create();
									}
									if (msg != null) {
										if (category != null) {
											if (category.StartsWith("!") && category.Length > 1) {
												string cat = category[1..];
												if (cat.Equals(msg.Category, StringComparison.OrdinalIgnoreCase)) {
													continue;
												}
											} else if (category.Equals(msg.Category, StringComparison.OrdinalIgnoreCase) == false) {
												continue;
											}
										}
										logs.Add(msg);
									}

								}
							}
						}

					} catch (Exception ex) {
						Console.WriteLine($"Error parsing log line: {ex.Message}");
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

		private async IAsyncEnumerable<BlobItem> GetSubscriptions() {
			foreach (var container in _containers) {
				await foreach (var sub in container.GetBlobsByHierarchyAsync(prefix: "resourceId=/SUBSCRIPTIONS")) {
					yield return sub.Blob;
				}
			}
		}



	}
}
