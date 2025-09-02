using Corner49.Infra.Logging;
using Microsoft.Azure.Amqp.Framing;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Corner49.Infra.Http {
	public class ApiClient {

		private readonly HttpClient _client;
		private bool _useAccessToken;

		private string _urlPrefix;
		protected JsonSerializerOptions _options;

		private ITelemetryService _telemetry;

		public ApiClient(string baseAddress, bool useAccessToken = false, Action<JsonSerializerOptions>? jsonOptions = null) {
			_useAccessToken = useAccessToken;

			var uri = new Uri(baseAddress);
			_urlPrefix = uri.PathAndQuery;
			if (!_urlPrefix.EndsWith("/")) _urlPrefix = _urlPrefix + "/";


			_client = new HttpClient();
			_client.BaseAddress = new Uri($"{uri.Scheme}://{uri.Host}");
			this.SetDefaultRequestHeaders(_client.DefaultRequestHeaders);



			_options = new System.Text.Json.JsonSerializerOptions {
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
				NumberHandling = JsonNumberHandling.AllowReadingFromString,
				DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
			};
			_options.Converters.Add(new JsonStringEnumConverter(_options.PropertyNamingPolicy));
			if (jsonOptions != null) jsonOptions(_options);

			_telemetry = new TelemetryService(InfraBuilder.Instance?.Configuration);
		}




		protected bool IncludeDataInMetrics { get; set; }
		protected bool EnsureSuccessStatusCode { get; set; } = true;

		#region OAuth

		private string? _accessToken;
		private DateTimeOffset _accessExpire = DateTimeOffset.UtcNow;

		private async Task<HttpClient> GetClient() {
			if (!_useAccessToken) return _client;

			if ((_accessToken == null) || (_accessExpire <= DateTimeOffset.UtcNow)) {
				await this.UpdateAccessToken(_accessToken);

				_client.DefaultRequestHeaders.Clear();
				if (_accessToken != null) {
					if (_accessToken.StartsWith("Bearer") || _accessToken.StartsWith("Basic")) {
						_client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", _accessToken);
					} else {
						_client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {_accessToken}");
					}
				}
				this.SetDefaultRequestHeaders(_client.DefaultRequestHeaders);
			}


			return _client;
		}

		protected virtual void SetDefaultRequestHeaders(HttpRequestHeaders headers) {
		}


		protected virtual Task UpdateAccessToken(string accessToken, int? expiresInSeconds = null) {
			_accessToken = accessToken;
			_accessExpire = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds ?? 3600);
			return Task.CompletedTask;
		}

		protected virtual void OnRequest(string method, string url, string? request, string? response, bool? isSuccess) {

		}


		#endregion


		#region Helper Methods

		public async Task<T?> Get<T>(string url, CancellationToken cancellationToken = default) where T : class {
			var path = this.CompleteUrl(url);

			using (var track = _telemetry.TrackDependency(this.GetType().Name, $"GET {path}")) {
				var request = new HttpRequestMessage(HttpMethod.Get, path);
				var client = await this.GetClient();
				using var resp = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

				if (resp.StatusCode == HttpStatusCode.NotFound) return null;

				string respData = null;
				try {
					respData = await resp.Content.ReadAsStringAsync();
					this.OnRequest("GET", path, null, respData, resp.IsSuccessStatusCode);

					if (this.EnsureSuccessStatusCode) resp.EnsureSuccessStatusCode();

					if (this.IncludeDataInMetrics) {
						track.Finish(true, respData);
					}

					return JsonSerializer.Deserialize<T>(respData, _options);
				} catch (HttpRequestException hre) {
					track.SetFailed(hre, new { Response = respData });

					throw new ApiClientException($"GET {client.BaseAddress}{path} failed : {hre.StatusCode} - {hre.Message}", hre);
				}

			}
		}


		public async Task<string?> Get(string url, CancellationToken cancellationToken = default) {
			var path = this.CompleteUrl(url);

			using (var track = _telemetry.TrackDependency(this.GetType().Name, $"GET {path}")) {
				var request = new HttpRequestMessage(HttpMethod.Get, path);
				var client = await this.GetClient();
				using var resp = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

				string respData = null;
				try {
					respData = await resp.Content.ReadAsStringAsync();
					this.OnRequest("GET", path, null, respData, resp.IsSuccessStatusCode);

					if (this.EnsureSuccessStatusCode) resp.EnsureSuccessStatusCode();

					if (this.IncludeDataInMetrics) {
						track.Finish(true, respData);
					}

					return respData;
				} catch (HttpRequestException hre) {
					track.SetFailed(hre, new { Response = respData });

					throw new ApiClientException($"GET {client.BaseAddress}{path} failed : {hre.StatusCode} - {hre.Message}", hre);
				}
			}
		}


		public async Task Delete(string url, CancellationToken cancellationToken = default) {
			var path = this.CompleteUrl(url);

			using (var track = _telemetry.TrackDependency(this.GetType().Name, $"DELETE {path}")) {
				var request = new HttpRequestMessage(HttpMethod.Delete, path);
				var client = await this.GetClient();
				using var resp = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
				if (this.EnsureSuccessStatusCode) resp.EnsureSuccessStatusCode();

				var respData = await resp.Content.ReadAsStringAsync();
				if (resp.IsSuccessStatusCode) return;
				track.SetFailed(null, new { Response = respData });
				throw new ApiClientException($"DELETE {client.BaseAddress}{path} Failed : {respData}", null);
			}
		}

		public async Task<T?> Put<TBody, T>(string url, TBody body, CancellationToken cancellationToken = default) where T : class where TBody : class {
			var path = this.CompleteUrl(url);

			using (var track = _telemetry.TrackDependency(this.GetType().Name, $"PUT {path}")) {
				var request = new HttpRequestMessage(HttpMethod.Put, path);
				string data = JsonSerializer.Serialize(body, _options);
				request.Content = new StringContent(data, System.Text.Encoding.UTF8, "application/json");

				var client = await this.GetClient();
				using var resp = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

				string respData = null;
				try {
					respData = await resp.Content.ReadAsStringAsync();
					this.OnRequest("PUT", path, data, respData, resp.IsSuccessStatusCode);

					if (this.EnsureSuccessStatusCode) resp.EnsureSuccessStatusCode();

					var rtrn = JsonSerializer.Deserialize<T>(respData, _options);
					if (this.IncludeDataInMetrics) {
						track.Finish(true, new { Request = data, Response = respData });
					}
					return rtrn;
				} catch (HttpRequestException hre) {
					track.SetFailed(hre, new { Request = data, Response = respData });
					throw new ApiClientException($"PUT {client.BaseAddress}{path} failed", hre);
				}
			}
		}

		public async Task<T?> Patch<TBody, T>(string url, TBody body, CancellationToken cancellationToken = default) where T : class where TBody : class {
			var path = this.CompleteUrl(url);

			using (var track = _telemetry.TrackDependency(this.GetType().Name, $"PATCH {path}")) {

				var request = new HttpRequestMessage(HttpMethod.Patch, path);
				string data = JsonSerializer.Serialize(body, _options);
				request.Content = new StringContent(data, System.Text.Encoding.UTF8, "application/json");

				var client = await this.GetClient();
				using var resp = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

				string? respData = null;
				try {
					if (this.EnsureSuccessStatusCode) resp.EnsureSuccessStatusCode();
					respData = await resp.Content.ReadAsStringAsync();
					this.OnRequest("PATCH", path, data, respData, resp.IsSuccessStatusCode);

					var rtrn = JsonSerializer.Deserialize<T>(respData, _options);
					if (this.IncludeDataInMetrics) {
						track.Finish(true, new { Request = data, Response = respData });
					}
					return rtrn;
				} catch (HttpRequestException hre) {
					track.SetFailed(hre, new { Request = data, Response = respData });
					throw new ApiClientException($"PATCH {client.BaseAddress}{path} failed : {hre.StatusCode} - {hre.Message}", hre);
				}
			}
		}


		public async Task<T?> Post<TBody, T>(string url, TBody body, CancellationToken cancellationToken = default) where T : class where TBody : class {
			var path = this.CompleteUrl(url);


			using (var track = _telemetry.TrackDependency(this.GetType().Name, $"POST {path}")) {
				var request = new HttpRequestMessage(HttpMethod.Post, path);

				string data = JsonSerializer.Serialize<TBody>(body, _options);
				request.Content = new StringContent(data, System.Text.Encoding.UTF8, "application/json");

				var client = await this.GetClient();
				using var resp = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

				string? respData = null;
				try {
					respData = await resp.Content.ReadAsStringAsync();
					this.OnRequest("POST", path, data, respData, resp.IsSuccessStatusCode);

					if (this.EnsureSuccessStatusCode) resp.EnsureSuccessStatusCode();
					var rtrn = JsonSerializer.Deserialize<T>(respData, _options);
					if (this.IncludeDataInMetrics) {
						track.Finish(true, new { Request = data, Response = respData });
					}
					return rtrn;

				} catch (HttpRequestException hre) {
					track.SetFailed(hre, new { Request = data, Response = respData });
					throw new ApiClientException($"POST {client.BaseAddress}{path} failed : {hre.StatusCode} - {hre.Message} - {respData}", hre);
				}
			}
		}

		public async Task<string?> Post<TBody>(string url, TBody body, CancellationToken cancellationToken = default) where TBody : class {
			var path = this.CompleteUrl(url);

			using (var track = _telemetry.TrackDependency(this.GetType().Name, $"POST {path}")) {
				var request = new HttpRequestMessage(HttpMethod.Post, path);
				string data = JsonSerializer.Serialize(body, _options);
				request.Content = new StringContent(data, System.Text.Encoding.UTF8, "application/json");

				var client = await this.GetClient();
				using var resp = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

				string? respData = null;
				try {
					respData = await resp.Content.ReadAsStringAsync();
					this.OnRequest("POST", path, data, respData, resp.IsSuccessStatusCode);

					if (this.EnsureSuccessStatusCode) resp.EnsureSuccessStatusCode();
					if (this.IncludeDataInMetrics) {
						track.Finish(true, data);
					}

					return respData;
				} catch (HttpRequestException hre) {
					track.SetFailed(hre, new { Request = data });
					throw new ApiClientException($"POST {client.BaseAddress}{path} failed : {hre.StatusCode} - {hre.Message}", hre);
				}
			}
		}

		public async Task Post(string url, string? body, CancellationToken cancellationToken = default) {
			var path = this.CompleteUrl(url);

			using (var track = _telemetry.TrackDependency(this.GetType().Name, $"POST {path}")) {
				var request = new HttpRequestMessage(HttpMethod.Post, path);
				if (body != null) request.Content = new StringContent(body, System.Text.Encoding.UTF8, "plain/text");

				var client = await this.GetClient();
				using var resp = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
				try {
					this.OnRequest("POST", path, null, null, resp.IsSuccessStatusCode);
					if (this.EnsureSuccessStatusCode) resp.EnsureSuccessStatusCode();
				} catch (HttpRequestException hre) {
					track.SetFailed(hre, null);
					throw new ApiClientException($"POST {client.BaseAddress}{path} failed : {hre.StatusCode} - {hre.Message}", hre);
				}
			}
		}


		public async Task<byte[]?> Download(string url, CancellationToken cancellationToken = default) {
			var path = this.CompleteUrl(url);

			using (var track = _telemetry.TrackDependency(this.GetType().Name, $"DOWNLOAD {path}")) {
				var request = new HttpRequestMessage(HttpMethod.Get, path);
				var client = await this.GetClient();
				using var resp = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

				try {
					if (this.EnsureSuccessStatusCode) resp.EnsureSuccessStatusCode();
					return await resp.Content.ReadAsByteArrayAsync();
				} catch (HttpRequestException hre) {
					track.SetFailed(hre, null);
					throw new ApiClientException($"DOWNLOAD {client.BaseAddress}{path} failed : {hre.StatusCode} - {hre.Message}", hre);
				}
			}
		}


		public async Task Download(string url, Stream target, CancellationToken cancellationToken = default) {
			var path = this.CompleteUrl(url);

			using (var track = _telemetry.TrackDependency(this.GetType().Name, $"DOWNLOAD {path}")) {
				var request = new HttpRequestMessage(HttpMethod.Get, path);
				var client = await this.GetClient();
				using var resp = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

				try {					
					if (this.EnsureSuccessStatusCode) resp.EnsureSuccessStatusCode();
					await resp.Content.CopyToAsync(target, cancellationToken);
				} catch (HttpRequestException hre) {
					track.SetFailed(hre, null);
					throw new ApiClientException($"DOWNLOAD {client.BaseAddress}{path} failed : {hre.StatusCode} - {hre.Message}", hre);
				}
			}
		}





		private string CompleteUrl(string url) {
			if (url.StartsWith("/")) {
				return _urlPrefix + url.Substring(1);
			} else {
				return _urlPrefix + url;
			}
		}

		#endregion


	}

}
