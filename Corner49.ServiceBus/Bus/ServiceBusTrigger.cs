using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Corner49.ServiceBus.Bus {

	public interface ICaptorServiceBusConfig {
		string ServiceBusConnection { get; set; }
		string QueueName { get; set; }

	}

	public class ServiceBusTrigger<T> : IHostedService, IDisposable where T : class, IServiceBusHandler {

		private readonly ILogger<T> _logger;
		private readonly IServiceProvider _serviceProvider;
		private readonly IServiceBusOptions _options;
		private readonly IServiceBusService _serviceBus;

		private ServiceBusProcessor? _busProcessor;

		public ServiceBusTrigger(ILogger<T> logger, IServiceProvider serviceProvider, IServiceBusService serviceBus, IServiceBusOptions options) {
			_logger = logger;
			_serviceProvider = serviceProvider;
			_serviceBus = serviceBus;

			_options = options;
		}



		public async Task StartAsync(CancellationToken stoppingToken) {
			_logger.LogInformation($"{_options.Name}.ServiceBusTrigger starting");
			_busProcessor = await _serviceBus.StartProcessor(_options, _busProcessor_ProcessMessageAsync, _busProcessor_ProcessErrorAsync);
		}

		public async Task StopAsync(CancellationToken stoppingToken) {
			_logger.LogInformation($"{_options.Name}.ServiceBusTrigger stopping");

			if (_busProcessor != null) {
				await _busProcessor.StopProcessingAsync(stoppingToken);
				_busProcessor.ProcessMessageAsync -= _busProcessor_ProcessMessageAsync;
				_busProcessor.ProcessErrorAsync -= _busProcessor_ProcessErrorAsync;
			}

		}

		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private bool _disposed;


		protected virtual void Dispose(bool disposing) {
			if (_disposed) return;

			if (_busProcessor != null) {
				_busProcessor.DisposeAsync();
				_busProcessor = null;
			}

			_disposed = true;
		}

		private Task _busProcessor_ProcessErrorAsync(ProcessErrorEventArgs arg) {
			try {
				_logger.LogError(arg.Exception, $"{_options.Name}.ServiceBusProcessor Error : {arg.Exception?.Message}");
			} catch (Exception err) {
				_logger.LogError(err, $"{_options.Name}.ServiceBusProcessor failed : {err.Message}");
				// Handle the exception from handler code
			}
			return Task.CompletedTask;
		}

		private async Task _busProcessor_ProcessMessageAsync(ProcessMessageEventArgs arg) {

			ServiceBusReceivedMessage message = arg.Message;
			var cmd = ServiceBusCommand.GetCommand(arg);


			if (_options.TrackMessageCount) {
				cmd.MessageCount = await _serviceBus.GetMessageCount(_options);
			}


			var activity = new Activity($"SB-GET {_options.Name}/{cmd.Name}/{cmd.MessageId}");
			if (message.ApplicationProperties.TryGetValue("Diagnostic-Id", out var objectId) && objectId is string diagnosticId) {
				activity.SetParentId(diagnosticId);
			}

			try {
				using (IServiceScope scope = _serviceProvider.CreateScope()) {
					var handler = ActivatorUtilities.CreateInstance<T>(scope.ServiceProvider);
					await handler.MessageReceived(cmd);
				}
			} catch (Exception err) {
				_logger.LogError(err, $"{_options.Name}.ProcessMessage failed : {err.Message}");
			} finally {
			}

		}
	}

}
