using Corner49.CLI.Jobs;
using Corner49.SampleCLI.Repos;
using System;
using System.Collections.Generic;
using System.Text;

namespace Corner49.SampleCLI.Jobs {
	internal class ListPartsJob : IJob {

		private readonly IDataRepo _repo;

		public ListPartsJob(IDataRepo repo) {
			_repo = repo;
		}

		public async Task Execute(string[]? args = null, CancellationToken cancellationToken = default) {
		
			
			var result = await  _repo.Query(c => c.Where(d => d.Name == "Fiat").OrderBy(d => d.Id));
			foreach(var item in result.Data) {
				Console.WriteLine($"Id: {item.Id}, Name: {item.Name}");
			};
		}

	}
}
