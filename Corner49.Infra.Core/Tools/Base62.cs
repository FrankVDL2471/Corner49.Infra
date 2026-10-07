using System.Numerics;

namespace Corner49.Infra.Tools {
	public static class Base62 {
		private static readonly string _chars = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
		public static string NewId() {
			Guid newGuid = Guid.NewGuid();
			return FromGuid(newGuid);
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
				result = _chars[(int)BigInteger.Remainder(value, 62)] + result;
				value = BigInteger.Divide(value, 62);
			}
			return result;
		}
		public static Guid ToGuid(string base62) {
			if (base62 == null) throw new ArgumentNullException(nameof(base62));
			BigInteger value = ToNumber(base62);
			byte[] bytes = value.ToByteArray();
			// If the byte array is larger than 16 bytes due to the added 0 during conversion to BigInteger, trim it.
			if (bytes.Length > 16) {
				bytes = bytes.Take(16).ToArray();
			}
			return new Guid(bytes.Reverse().ToArray());
		}
		public static BigInteger ToNumber(string base62) {
			base62 = base62.ToLower();
			BigInteger result = new BigInteger(0);
			BigInteger multiplier = new BigInteger(1);
			for (int i = base62.Length - 1; i >= 0; i--) {
				int value = _chars.IndexOf(base62[i]);
				if (value < 0) throw new ArgumentException("Invalid character in Base62 string.", nameof(base62));
				result += value * multiplier;
				multiplier *= 62;
			}
			return result;
		}
	}
}
