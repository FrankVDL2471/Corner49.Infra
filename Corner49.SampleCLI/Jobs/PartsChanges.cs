using Corner49.Infra;
using Corner49.SampleCLI.Repos;
using System;
using System.Collections.Generic;
using System.Text;

namespace Corner49.SampleCLI.Jobs {
	internal class PartsChanges : DocumentJob<DataModel> {

		public PartsChanges(IServiceProvider serviceProvider) : base(serviceProvider) {
			this.DatabaseName = "dev-Ottogusto";
			this.ContainerName = "Parts";
		}
		
		public override Task OnChange(IReadOnlyList<DataModel> inputs) {
			foreach (var item in inputs) {
				Console.WriteLine("Item Changed : " + item.Name);
			}	
			return Task.CompletedTask;
		}	
	}
}
