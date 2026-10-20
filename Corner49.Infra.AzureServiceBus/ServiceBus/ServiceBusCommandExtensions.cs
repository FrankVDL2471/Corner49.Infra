using System;
using System.Collections.Generic;
using System.Text;
using System.Web;

namespace Corner49.Infra.ServiceBus {

	public static class ServiceBusCommandExtensions {


		public static string? GetId(this ServiceBusCommand cmd) {
			int idx = cmd.Name.IndexOf("?");
			if (idx <= 0) return cmd.Name;

			return cmd.Name.Substring(0, idx);
		}


		public static string? GetArg(this ServiceBusCommand cmd, string argName) {
			int idx = cmd.Name.IndexOf("?");
			string args = (idx > 0) ? cmd.Name.Substring(idx + 1) : cmd.Name;


			Uri myUri = new Uri($"http://localhost?{args}");
			return HttpUtility.ParseQueryString(myUri.Query).Get(argName);
		}


	}
}
