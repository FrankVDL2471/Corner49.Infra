using SkiaSharp;

namespace Corner49.Infra.Tools {
	public static class ImageHelper {


		public static async Task<byte[]?> GetAvatar(string name, bool isGuest = true) {
			if (string.IsNullOrEmpty(name)) return null;

			string url = "https://ui-avatars.com/api/?background=random&bold=true";
			if (isGuest) {
				url += "&rounded=true";
			} else {
				url += "&size=128&length=4&font-size=0.33";
			}
			url += "&name=" + Uri.EscapeDataString(name);


			HttpClient http = new HttpClient();
			return await http.GetByteArrayAsync(url);
		}


		public static string ResizeImage(string imageData, int width, int height) {
			var bmp = Load(imageData);
			var img = ResizeImage(bmp, width, height);
			return Save(img);
		}

		public static byte[] Load(string imageData) {
			string[] parts = imageData.Split(',');

			byte[] buffer;
			if (parts.Length > 1) {
				buffer = Convert.FromBase64String(parts[1]);
			} else {
				buffer = Convert.FromBase64String(imageData);
			}
			return buffer;
		}
		public static byte[] ResizeImage(byte[] imageData, float maxWidth, float maxHeight, SKSamplingOptions? sampling = null) {
			MemoryStream ms = new MemoryStream(imageData);
			return ResizeImage(ms, maxWidth, maxHeight, sampling);
		}

		public static byte[] ResizeImage(Stream ms, float maxWidth, float maxHeight, SKSamplingOptions? sampling = null) {
			SKBitmap sourceBitmap = SKBitmap.Decode(ms);

			// Use default medium quality sampling if not specified
			sampling ??= new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);


			float sourceRatio = (float)sourceBitmap.Width / sourceBitmap.Height;

			// To preserve the aspect ratio
			float ratioX = (float)maxWidth / (float)sourceBitmap.Width;
			float ratioY = (float)maxHeight / (float)sourceBitmap.Height;
			float ratio = Math.Min(ratioX, ratioY);


			// New width and height based on aspect ratio
			int width = (int)(sourceBitmap.Width * ratio);
			int height = (int)(sourceBitmap.Height * ratio);

			var imageInfo = new SKImageInfo(width, height);
			SKBitmap scaledBitmap = sourceBitmap.Resize(imageInfo, sampling.Value);
			SKImage scaledImage = SKImage.FromBitmap(scaledBitmap);

			SKData data = scaledImage.Encode();

			return data.ToArray();
		}



		public static string Save(byte[] buffer) {
			return "data:image/jpeg;base64," + Convert.ToBase64String(buffer);
		}
		public static string Save(Stream data) {
			byte[] buffer = new byte[data.Length];
			data.Read(buffer, 0, buffer.Length);
			return Save(buffer);
		}
		public static string Save(Stream data, float maxWidth, float maxHeight) {
			// Use high quality sampling (equivalent to old SKFilterQuality.High)
			var highQuality = new SKSamplingOptions(SKCubicResampler.Mitchell);
			byte[] buffer = ResizeImage(data, maxWidth, maxHeight, highQuality);
			return Save(buffer);
		}

	}
}
