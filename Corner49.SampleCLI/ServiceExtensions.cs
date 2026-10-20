using Corner49.Infra;
using System;
using System.Collections.Generic;
using System.Text;

namespace Corner49.SampleCLI {
	internal static class ServiceExtensions {

		public static IInfraBuilder Addservices(this IInfraBuilder infra) {
			var redis = infra.Configuration["Redis:Data:ConnectString"];
			return infra;
		}
	}
}
