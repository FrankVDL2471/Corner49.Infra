using System.Globalization;

namespace Corner49.Infra.Tools {
	public static class Generator {

		public static string NewId() {
			return Base36.NewShortId();
		}

		public static string GetCRC(string input) {
			int val = 0;
			foreach (char c in input) {
				val += (int)c;
			}
			return (val % 1000).ToString().PadLeft(3, '0');
		}


		public static string ToShort(this Guid input) {
			var unixMs = ((long)(DateTime.Now - new DateTime(1970, 1, 1)).TotalMilliseconds).ToString();
			var dec = long.Parse(input.ToString().Split('-')[0], NumberStyles.HexNumber);

			var uid = long.Parse($"{dec}{unixMs.Substring(unixMs.Length - 6)}");

			var encoded = Convert.ToBase64String(BitConverter.GetBytes(uid));
			encoded = encoded
					.Replace("=", "")
					.Replace("+", "z")
					.Replace("/", "Z")
					.Replace("O", "1")
					.Replace("l", "L");

			return encoded.Substring(0, 10);  //last char is alway 'A'
		}


	}
}
