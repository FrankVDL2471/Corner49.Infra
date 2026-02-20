using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Amqp.Framing;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace Corner49.Infra.Storage {
	public interface IBlobService {


		Task<bool> Exists(string containerName, string name);

		Task<Stream?> Read(string containerName, string name);

		Task<Stream?> Write(string containerName, string name, string? contentType = null, Dictionary<string, string>? metaData = null);

		Task<bool> SetMeta(string containerName, string name, string? contentType = null, Dictionary<string, string>? metaData = null);

		Task<string?> Upload(string containerName, IFormFile file, string? name = null, Dictionary<string, string>? metaData = null);
		Task<string?> Upload(string containerName, string name, Stream data, string? contentType = null, Dictionary<string, string>? metaData = null);
		Task<string?> Upload(string containerName, string name, byte[] data, string? contentType = null, Dictionary<string, string>? metaData = null);
		Task<string?> UploadBase64(string containerName, string name, string data, string? contentType = null, Dictionary<string, string>? metaData = null);

		Task<string?> Append(string containerName, string name, Stream data, string? contentType = null, Dictionary<string, string>? metaData = null);


		Task<string?> CreateText(string containerName, string name);
		Task<string?> AppendText(string containerName, string name, string text);

		Task<bool> Delete(string containerName, string name);

		Task<BlobInfo?> GetBlob(string containerName, string name, Stream target);

		Task<string?> GetFile(string containerName, string name, Stream target);
		Task<string?> GetFileInBase64(string containerName, string name);

		Task<Stream?> DownloadFile(string containerName, string name, CancellationToken cancellationToken = default);
		Task<string?> MoveFile(string sourceContainer, string sourceName, string targetContainer, string targetName);
		Task<IEnumerable<string>?> GetFiles(string containerName);
		string? GetCDN(string containName, string name);
	}

	public class BlobService : IBlobService {

		private readonly string? _connectionString;
		private readonly string? _cdnHost;

		public BlobService(string name, IConfiguration config) {
			_connectionString = config.GetSection($"Storage:{name}:ConnectString")?.Value;
			_cdnHost = config.GetSection($"Storage:{name}:CDN")?.Value;
		}
		public BlobService(string connectString) {
			_connectionString = connectString;
		}



		private readonly List<string> _containers = new List<string>();

		public async Task<BlobContainerClient?> GetContainer(string containerName, bool createIfNotExists) {
			string nm = FormatContainerName(containerName);

			BlobServiceClient srv = new BlobServiceClient(_connectionString);

			if (_containers.Contains(nm)) {
				return new BlobContainerClient(_connectionString, nm);
			}

			if (createIfNotExists) {
				try {
					// Create the container
					BlobContainerClient container = await srv.CreateBlobContainerAsync(nm, Azure.Storage.Blobs.Models.PublicAccessType.Blob);
					if (await container.ExistsAsync()) {
						Console.WriteLine("Created container {0}", container.Name);
						_containers.Add(nm);
						return container;
					}
				} catch (RequestFailedException e) {
					Console.WriteLine("HTTP error code {0}: {1}", e.Status, e.ErrorCode);
					Console.WriteLine(e.Message);
				}
			}
			var client = new BlobContainerClient(_connectionString, nm);
			if (_containers.Contains(nm)) {
				return client;
			}
			if (await client.ExistsAsync()) {
				_containers.Add(nm);
				return client;
			}
			return null;
		}






		public async Task<IList<string>> GetBlobNames(string containerName) {
			List<string> nms = new List<string>();
			var client = await GetContainer(containerName, false);
			if (client != null) {
				await foreach (var blob in client.GetBlobsAsync()) {
					nms.Add(blob.Name);
				}
			}
			return nms;
		}




		public async Task<bool> Exists(string containerName, string name) {
			string nm = FormatContainerName(containerName);

			BlobServiceClient srv = new BlobServiceClient(_connectionString);
			var client = new BlobContainerClient(_connectionString, nm);

			if (!_containers.Contains(nm)) {
				if (!await client.ExistsAsync()) return false;
			}

			var file = client.GetBlobClient(name);
			return await file.ExistsAsync();
		}


		public async Task<Stream?> Read(string containerName, string name) {
			var container = await GetContainer(containerName, false);
			if (container == null) return null;

			var client = container.GetBlobClient(name);
			if (!await client.ExistsAsync()) return null;
			return await client.OpenReadAsync();
			
		}

		public async Task<bool> SetMeta(string containerName, string name, string? contentType = null, Dictionary<string, string>? metaData = null) {
			var container = await GetContainer(containerName, false);
			if (container == null) return false;

			var client = container.GetBlobClient(name);
			if (await client.ExistsAsync()) {
				if (!string.IsNullOrEmpty(contentType)) {
					var headers = new Azure.Storage.Blobs.Models.BlobHttpHeaders();
					headers.ContentType = contentType;
					await client.SetHttpHeadersAsync(headers);
				}
				if (metaData != null) {
					await client.SetMetadataAsync(metaData);
				}
				return true;
			}
			return false;
		}


		public async Task<Stream?> Write(string containerName, string name, string? contentType = null, Dictionary<string, string>? metaData = null) {
			var container = await GetContainer(containerName, true);
			if (container == null) return null;

			var client = container.GetBlobClient(name);
			if (!string.IsNullOrEmpty(contentType)) {
				var headers = new Azure.Storage.Blobs.Models.BlobHttpHeaders();
				headers.ContentType = contentType;
				await client.SetHttpHeadersAsync(headers);
			}
			if (metaData != null) {
				await client.SetMetadataAsync(metaData);
			}

			return await client.OpenWriteAsync(true);
		}

		public async Task<string?> Upload(string containerName, IFormFile file, string? name = null, Dictionary<string, string>? metaData = null) {
			var container = await GetContainer(containerName, true);
			if (container == null) return null;

			string blobName = name ?? file.FileName;
			var client = container.GetBlobClient(blobName);

			await client.DeleteIfExistsAsync();

			using (Stream data = file.OpenReadStream()) {
				await client.UploadAsync(data);
			}
			var headers = new Azure.Storage.Blobs.Models.BlobHttpHeaders();
			headers.ContentType = file.ContentType;
			headers.ContentDisposition = file.ContentDisposition;
			await client.SetHttpHeadersAsync(headers);

			if (metaData != null) {
				await client.SetMetadataAsync(metaData); 
			}


			return GetCDN(containerName, blobName) + $"?v={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
		}


		public async Task<string?> Upload(string containerName, string name, Stream data, string? contentType = null, Dictionary<string, string>? metaData = null) {
			var container = await GetContainer(containerName, true);
			if (container == null) return null;	

			var client = container.GetBlobClient(name);
			await client.DeleteIfExistsAsync();

			await client.UploadAsync(data);

			if (!string.IsNullOrEmpty(contentType)) {
				var headers = new Azure.Storage.Blobs.Models.BlobHttpHeaders();
				headers.ContentType = contentType;
				await client.SetHttpHeadersAsync(headers);
			}
			if (metaData != null) {
				await client.SetMetadataAsync(metaData);
			}

			return GetCDN(container.Name, name);
		}

		public async Task<string?> Upload(string containerName, string name, byte[] data, string? contentType = null, Dictionary<string, string>? metaData = null) {
			var container = await GetContainer(containerName, true);
			if (container == null) return null;

			var client = container.GetBlobClient(name);
			await client.DeleteIfExistsAsync();

			await client.UploadAsync(new BinaryData(data));

			if (!string.IsNullOrEmpty(contentType)) {
				var headers = new Azure.Storage.Blobs.Models.BlobHttpHeaders();
				headers.ContentType = contentType;
				await client.SetHttpHeadersAsync(headers);
			}
			if (metaData != null) {
				await client.SetMetadataAsync(metaData);
			}


			return GetCDN(container.Name, name);
		}


		public Task<string?> UploadBase64(string containerName, string name, string data, string? contentType = null, Dictionary<string, string>? metaData = null) {
			byte[] buff = Convert.FromBase64String(data);
			return this.Upload(containerName, name, buff, contentType);
		}


		public async Task<string?> Append(string containerName, string name, Stream data, string? contentType = null, Dictionary<string, string>? metaData = null) {
			var container = await GetContainer(containerName, true);
			if (container == null) return null;


			AppendBlobClient client = container.GetAppendBlobClient(name);
			await client.CreateIfNotExistsAsync();

			int maxBlockSize = client.AppendBlobMaxAppendBlockBytes;
			long bytesLeft = data.Length;
			byte[] buffer = new byte[maxBlockSize];
			while (bytesLeft > 0) {
				int blockSize = (int)Math.Min(bytesLeft, maxBlockSize);
				int bytesRead = await data.ReadAsync(buffer.AsMemory(0, blockSize));
				await using (MemoryStream memoryStream = new MemoryStream(buffer, 0, bytesRead)) {
					await client.AppendBlockAsync(memoryStream);
				}
				bytesLeft -= bytesRead;
			}
			return GetCDN(container.Name, name);
		}


		public async Task<string?> CreateText(string containerName, string name) {
			var container = await GetContainer(containerName, true);
			if (container == null) return null;

			AppendBlobClient client = container.GetAppendBlobClient(name);
			await client.CreateIfNotExistsAsync();

			return GetCDN(container.Name, name);
		}

		public async Task<string?> AppendText(string containerName, string name, string text) {
			var container = await GetContainer(containerName, false);
			if (container == null) return null;

			int cnt = 0;
			string fileName = name;
			while (true) {
				AppendBlobClient client = container.GetAppendBlobClient(fileName);
				var props = await client.GetPropertiesAsync();
				if (props.Value.BlobCommittedBlockCount >= client.AppendBlobMaxBlocks) {
					cnt++;
					fileName += "-part" + cnt;
					continue;
				}


				using (MemoryStream mem = new MemoryStream()) {
					using (StreamWriter writer = new StreamWriter(mem, System.Text.Encoding.UTF8, leaveOpen: true)) {
						await writer.WriteLineAsync(text);
					}
					mem.Position = 0;

					await client.AppendBlockAsync(mem);
				}
				break;
			}



			return GetCDN(container.Name, fileName);
		}



		public async Task<bool> Delete(string containerName, string name) {
			var container = await GetContainer(containerName, false);
			if (container == null) return false;

			var client = container.GetBlobClient(name);
			return await client.DeleteIfExistsAsync(Azure.Storage.Blobs.Models.DeleteSnapshotsOption.IncludeSnapshots);
		}

		public async Task<BlobInfo?> GetBlob(string containerName, string name, Stream target) {
			var container = await GetContainer(containerName, false);
			if (container == null) return null;

			var client = container.GetBlobClient(name);
			if (await client.ExistsAsync()) {
				var response = await client.DownloadToAsync(target);
				target.Position = 0;

				BlobInfo info = new BlobInfo();
				info.ETag = response.Headers.ETag.ToString();
				info.ContentType = response.Headers.ContentType;		
				info.ContentLength = response.Headers.ContentLengthLong ?? response.Headers.ContentLength;
				info.Date = response.Headers.Date;

				return info;
			}
			return null;
		}


		public async Task<string?> GetFile(string containerName, string name, Stream target) {
			var container = await GetContainer(containerName, false);
			if (container == null) return null;

			var client = container.GetBlobClient(name);
			if (await client.ExistsAsync()) {
				var response = await client.DownloadToAsync(target);
				target.Position = 0;
				return response.Headers.ContentType;
			}
			return null;
		}


		public async Task<string?> GetFileInBase64(string containerName, string name) {
			var container = await GetContainer(containerName, false);
			if (container == null) return null;

			var client = container.GetBlobClient(name);
			if (await client.ExistsAsync()) {
				var response = await client.DownloadContentAsync();
				return Convert.ToBase64String(response.Value.Content);
			}
			return null;
		}

		public async Task<Stream?> DownloadFile(string containerName, string name, CancellationToken cancellationToken = default) {
			var container = await GetContainer(containerName,false);
			if (container == null) return null;
			var client = container.GetBlobClient(name);

			if (!await client.ExistsAsync()) return null;


			var result = await client.DownloadStreamingAsync(null, cancellationToken);
			return result?.Value?.Content;

		}



		public async Task<string?> MoveFile(string sourceContainer, string sourceName, string targetContainer, string targetName) {
			var source = await GetContainer(sourceContainer, false);
			if (source == null) return null;
			var sourceClient = source.GetBlobClient(sourceName);

			if (!await sourceClient.ExistsAsync()) return null;
			

			var target = await GetContainer(targetContainer, true);
			var targetClient = target.GetBlobClient(targetName);
			await targetClient.DeleteIfExistsAsync();

			using (var stream = await targetClient.OpenWriteAsync(true)) {
				await sourceClient.DownloadToAsync(stream);
				await stream.FlushAsync();
			}
			

			await sourceClient.DeleteIfExistsAsync();

			return this.GetCDN(targetContainer, targetName);
		}






		public async Task<IEnumerable<string>?> GetFiles(string containerName) {
			var container = await GetContainer(containerName, false);
			if (container == null) return null;

			return container.GetBlobs().Select(b => b.Name);
		}

		public string GetCDN(string containerName, string name) {
			return $"{_cdnHost}/{containerName}/{name}";
		}


		public string FormatContainerName(string name) {
			StringBuilder bld = new StringBuilder();
			foreach (char ch in name) {
				if (ch == '_') continue;
				if (ch >= 65 && ch <= 90) {
					bld.Append((char)(ch + 32));
					bld.Append((char)(ch + 32));
				} else {
					bld.Append(ch);
				}
			}
			return bld.ToString();

		}



	}
}
