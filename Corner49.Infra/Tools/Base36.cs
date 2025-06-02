using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;

namespace Corner49.Infra.Tools {
	public static class Base36 {
		private static readonly string _chars = "0123456789abcdefghijklmnopqrstuvwxyz";
		public static string NewId() {
			Guid newGuid = Guid.NewGuid();
			return FromGuid(newGuid);
		}

		public static string NewShortId() {
			long unixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			int random = RandomNumberGenerator.GetInt32(int.MaxValue);


			string p1 = FromNumber(unixMs);
			string p2 = FromNumber(random);

			return $"{p1}{p2}";
		}

		public static string FromGuid(Guid guid) {
			var bytes = guid.ToByteArray().Reverse().ToArray();
			var bigintBytes = new byte[bytes.Length + 1];
			bytes.CopyTo(bigintBytes, 0);
			BigInteger value = new BigInteger(bigintBytes);
			return FromNumber(value);
		}




		public static string FromNumber(BigInteger value) {
			if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), value, "value must be positive.");
			if (value == 0) return "0";
			string result = "";
			while (value > 0) {
				result = _chars[(int)BigInteger.Remainder(value, 36)] + result;
				value = BigInteger.Divide(value, 36);
			}
			return result;
		}
		public static Guid ToGuid(string base36) {
			if (base36 == null) throw new ArgumentNullException(nameof(base36));
			BigInteger value = ToNumber(base36);
			byte[] bytes = value.ToByteArray();
			// If the byte array is larger than 16 bytes due to the added 0 during conversion to BigInteger, trim it.
			if (bytes.Length > 16) {
				bytes = bytes.Take(16).ToArray();
			}
			return new Guid(bytes.Reverse().ToArray());
		}
		public static BigInteger ToNumber(string base36) {
			base36 = base36.ToLower();
			BigInteger result = new BigInteger(0);
			BigInteger multiplier = new BigInteger(1);
			for (int i = base36.Length - 1; i >= 0; i--) {
				int value = _chars.IndexOf(base36[i]);
				if (value < 0) throw new ArgumentException("Invalid character in base36 string.", nameof(base36));
				result += value * multiplier;
				multiplier *= 36;
			}
			return result;
		}
	}
}
