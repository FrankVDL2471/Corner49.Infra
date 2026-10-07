namespace Corner49.Infra.Tools {
	public class Hasher {


		private readonly static List<char> HashSet = new List<char>("abcdefghijklmnopqrstuvwxyz0123456789@.+-_".ToCharArray());

		private readonly static List<char> HashReplace = new List<char>("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNO".ToCharArray());



		public static string GetEmailHash(string email) {

			string text = email.Trim().ToLower();
			char[] code = new char[Math.Max(text.Length, 14)];

			int offset = text.Length;
			for (int i = 0; i < code.Length; i++) {
				int idx = HashSet.IndexOf(text[i % text.Length]);
				if (idx < 0) continue;
				code[i] = HashReplace[(idx + offset) % HashReplace.Count];
				offset = Math.Max(idx, 0);
			}
			return new string(code);

		}
	}
}
