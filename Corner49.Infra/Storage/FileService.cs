using Azure.Storage.Files.Shares.Models;
using Azure.Storage.Files.Shares;
using Azure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Corner49.Infra.Storage {

	public class FileService {

		private readonly string _connectString;
		private readonly string _root;

		private readonly ShareClient _client;
		public FileService(string connectString, string root) {
			_connectString = connectString;// config["Storage:Data:ConnectString"];
			_root = root;


			_client = new ShareClient(connectString, root.ToLower());
			_client.CreateIfNotExists();
		}

		public async Task<bool> Updload(string path, string fileName, Stream data) {
			var dir = await GetDirectory(path);

			ShareFileClient file = dir.GetFileClient(fileName);
			await file.DeleteIfExistsAsync();

			file.Create(data.Length);
			file.UploadRange(new HttpRange(0, data.Length), data);

			return true;
		}

		public async Task<bool> Download(string path, string fileName, Stream target) {
			var dir = await GetDirectory(path);

			ShareFileClient file = dir.GetFileClient(fileName);
			if (!await file.ExistsAsync()) return false;

			ShareFileDownloadInfo download = file.Download();
			await download.Content.CopyToAsync(target);

			return true;
		}

		public async Task<Stream?> Read(string path, string fileName) {
			var dir = await GetDirectory(path);

			ShareFileClient file = dir.GetFileClient(fileName);
			if (!await file.ExistsAsync()) return null;

			return await file.OpenReadAsync(new ShareFileOpenReadOptions(false));
		}

		public async Task<Stream> Write(string path, string fileName, long size) {
			var dir = await GetDirectory(path);

			ShareFileClient file = dir.GetFileClient(fileName);
			return await file.OpenWriteAsync(true, 0, new ShareFileOpenWriteOptions { MaxSize = size });
		}





		private async Task<ShareDirectoryClient> GetDirectory(string path) {
			string[] parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
			ShareDirectoryClient dir = _client.GetRootDirectoryClient();
			foreach (var nm in parts) {
				dir = dir.GetSubdirectoryClient(nm);
				await dir.CreateIfNotExistsAsync();
			}
			return dir;
		}

	}
}

