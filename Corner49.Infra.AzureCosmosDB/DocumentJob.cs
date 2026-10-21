using Corner49.Infra.DB;
using Corner49.Infra.Jobs;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace Corner49.Infra {
	public abstract class  DocumentJob<T> : IJob where T: class{


		private readonly IDocumentDB _db;

		public DocumentJob(IServiceProvider services) {
			_db = services.GetRequiredService<IDocumentDB>();
		}


		public string? DatabaseName { get; set; }

		public string? ContainerName { get; set; }

		public abstract Task OnChange(T item);

		public async Task Execute(string[]? args = null, CancellationToken cancellationToken = default) {
			if (this.ContainerName == null) {
				throw new ArgumentException("ContainerName must be specified.");
			} 


			var repo = string.IsNullOrEmpty(this.DatabaseName) ?  _db.GetRepo<T>(ContainerName) : _db.GetRepo<T>(DatabaseName, ContainerName);	
			if (!await repo.Exists()) {
				throw new ArgumentException($"Container '{this.ContainerName}' does not exist in database '{this.DatabaseName}'.");
			}

			string leaseId = $"{this.GetType().Name}-{this.GetType().Assembly.GetName().Name}";
			await foreach(T item in repo.GetChanges(leaseId, "changeLeases", cancellationToken)) {
				await this.OnChange(item);
			}
		}
	}
}
