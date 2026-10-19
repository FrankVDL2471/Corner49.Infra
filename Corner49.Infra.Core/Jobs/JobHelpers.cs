using System;
using System.Collections.Generic;
using System.Text;

namespace Corner49.Infra.Jobs {
	public static class JobHelpers {


		public static Dictionary<string, string> ParseArgs(string[] args) {
			var dict = new Dictionary<string, string>();
			foreach (var arg in args) {
				if (arg.StartsWith("--")) arg.Substring(2);

				var parts = arg.Split('=', 2);
				if (parts.Length == 2) {
					dict[parts[0]] = parts[1].TrimStart('"').TrimEnd('"');
				} else {
					dict[parts[0]] = "true";
				}

			}
			return dict;
		}

		public static string? GetArg(string[]? args, string key, string? defaultValue = null) {
			if (args?.Any() != true) return defaultValue;

			var dict = ParseArgs(args);
			if (dict.TryGetValue(key, out var value)) {
				return value;
			}
			return defaultValue;
		}
	}
}
