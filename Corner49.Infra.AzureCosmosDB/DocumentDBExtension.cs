using Corner49.Infra.DB;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;

namespace Corner49.Infra {
	public class DocumentDBExtension : InfraExtension {


		private DocumentDBBuilder? _docDBBuilder = null;
		private Action<DocumentDBBuilder>? _repos = null;


		public DocumentDBExtension(Action<DocumentDBBuilder>? repos = null) {
			_repos = repos;
		}


		public override Task Build(IInfraBuilder infra, IConfiguration config) {
			_docDBBuilder = infra.Services.AddDocumentDB(config, _repos);

			try {
				if (!Debugger.IsAttached) {
					Type defaultTrace = Type.GetType("Microsoft.Azure.Cosmos.Core.Trace.DefaultTrace,Microsoft.Azure.Cosmos.Direct");
					TraceSource traceSource = (TraceSource)defaultTrace.GetProperty("TraceSource").GetValue(null);
					if (traceSource?.Listeners != null) traceSource.Listeners.Remove("Default");
				}
			} catch (Exception ex) {
				Console.WriteLine($"Error disabling CosmosDB default trace: {ex.Message}");
			}

			return Task.CompletedTask;
		}


		public override async Task Start(IServiceProvider serviceProvider) {
			if (_docDBBuilder != null) await _docDBBuilder.Init(serviceProvider);
		}


	}
}
