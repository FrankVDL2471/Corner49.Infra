using SkiaSharp;

namespace Corner49.Infra.Helpers {
	public static class ImageHelper {

		public static (byte[] data, int Height, int Width)? Resize(byte[] fileContents, int maxWidth, int maxHeight, int quality = 80, string format = "png", bool onlyDownsize = false) {
			using MemoryStream ms = new MemoryStream(fileContents);
			using SKBitmap sourceBitmap = SKBitmap.Decode(ms);

			if (onlyDownsize) {
				if ((sourceBitmap.Width < maxWidth) && (sourceBitmap.Height < maxHeight)) return null;
			}

			SKEncodedImageFormat ext = SKEncodedImageFormat.Png;
			Enum.TryParse<SKEncodedImageFormat>(format, true, out ext);


			// To preserve the aspect ratio
			float ratioX = (float)maxWidth / (float)sourceBitmap.Width;
			float ratioY = (float)maxHeight / (float)sourceBitmap.Height;
			float ratio = Math.Min(ratioX, ratioY);

			float sourceRatio = (float)sourceBitmap.Width / sourceBitmap.Height;

			// New width and height based on aspect ratio
			int width = (int)(sourceBitmap.Width * ratio);
			int height = (int)(sourceBitmap.Height * ratio);

			using SKBitmap scaledBitmap = sourceBitmap.Resize(new SKImageInfo(width, height), SKSamplingOptions.Default);
			using SKImage scaledImage = SKImage.FromBitmap(scaledBitmap);
			using SKData data = scaledImage.Encode(ext, quality);

			

			return (data.ToArray(), height, width);
		}

		public static (int Height, int Width)? Resize(Stream source, int maxWidth, int maxHeight, Stream target, int quality = 80, string format = "png", bool onlyDownsize = false) {
			using SKBitmap sourceBitmap = SKBitmap.Decode(source);

			if (onlyDownsize) {
				if ((sourceBitmap.Width < maxWidth) && (sourceBitmap.Height < maxHeight)) return null;
			}

			SKEncodedImageFormat ext = SKEncodedImageFormat.Png;
			Enum.TryParse<SKEncodedImageFormat>(format, true, out ext);


			// To preserve the aspect ratio
			float ratioX = (float)maxWidth / (float)sourceBitmap.Width;
			float ratioY = (float)maxHeight / (float)sourceBitmap.Height;
			float ratio = Math.Min(ratioX, ratioY);

			float sourceRatio = (float)sourceBitmap.Width / sourceBitmap.Height;

			// New width and height based on aspect ratio
			int width = (int)(sourceBitmap.Width * ratio);
			int height = (int)(sourceBitmap.Height * ratio);

			using SKBitmap scaledBitmap = sourceBitmap.Resize(new SKImageInfo(width, height), SKSamplingOptions.Default);
			using SKImage scaledImage = SKImage.FromBitmap(scaledBitmap);
			using SKData data = scaledImage.Encode(ext, quality);


			data.SaveTo(target);

			return (height, width);
		}


		public static void Convert(Stream source, Stream target, int quality = 80, string format = "png") {
			using SKBitmap sourceBitmap = SKBitmap.Decode(source);

			SKEncodedImageFormat ext = SKEncodedImageFormat.Png;
			Enum.TryParse<SKEncodedImageFormat>(format, true, out ext);

			using SKData data = sourceBitmap.Encode(ext, quality);
			data.SaveTo(target);
		}





		public static string Resize(string imageData, int maxWidth, int maxHeight) {
			string data = imageData;
			string contentType = null;
			if (data.StartsWith("data:")) {
				int idx = imageData.IndexOf(',');
				contentType = imageData.Substring(5, idx - 12);
				data = imageData.Substring(idx + 1);
			}

			byte[] bytes = System.Convert.FromBase64String(data);

			var resize = Helpers.ImageHelper.Resize(bytes, maxWidth, maxHeight);
			if (resize == null) return null;

			return "data:" + contentType + ";base64," + System.Convert.ToBase64String(resize?.data!);

		}
	}
}
