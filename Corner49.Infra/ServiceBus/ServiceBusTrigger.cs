using Azure.Messaging.ServiceBus;
using Corner49.Infra.Health;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Corner49.Infra.ServiceBus {

	public interface ICaptorServiceBusConfig {
		string ServiceBusConnection { get; set; }
		string QueueName { get; set; }

	}

	public class ServiceBusTrigger<T> : IHealthStatus, IHostedService, IDisposable where T : class, IServiceBusHandler {

		private readonly ILogger<T> _logger;
		private readonly IServiceProvider _serviceProvider;
		private readonly IServiceBusOptions _options;
		private readonly IServiceBusService _serviceBus;
		private readonly TelemetryClient? _telemetry;

		private ServiceBusProcessor? _busProcessor;

		public ServiceBusTrigger(ILogger<T> logger, TelemetryClient? telemetry, IServiceProvider serviceProvider, IServiceBusService serviceBus, IServiceBusOptions options) {
			_logger = logger;
			_telemetry = telemetry;
			_serviceProvider = serviceProvider;
			_serviceBus = serviceBus;

			_options = options;

			HealthService.AddCheck(this);
		}


		string IHealthStatus.Name => $"ServiceBusTrigger({_options?.Name})";

		bool IHealthStatus.IsRunning => _busProcessor?.IsProcessing ?? false;


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

			var opp = _telemetry != null ? _telemetry.StartOperation<RequestTelemetry>(activity) : null;
			try {
				using (IServiceScope scope = _serviceProvider.CreateScope()) {
					var handler = ActivatorUtilities.CreateInstance<T>(scope.ServiceProvider);
					await handler.MessageReceived(cmd);
				}
			} catch (Exception err) {
				if (opp != null) opp.Telemetry.Success = false;
				if (_telemetry != null) _telemetry.TrackException(err);
				_logger.LogError(err, $"{_options.Name}.ProcessMessage failed : {err.Message}");
			} finally {
				if (opp != null) opp.Dispose();
			}
			
		}
	}

}
