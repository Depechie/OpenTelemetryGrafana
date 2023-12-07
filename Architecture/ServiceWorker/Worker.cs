using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QueueCommon;
using QueueCommon.Models.Interfaces;
using QueueCommon.Models;
using System.Diagnostics;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry;
using System.Collections.Generic;
using System.Text;
using System;
using RabbitMQ.Client;
using System.Linq;
using RabbitMQ.Client.Events;

namespace ServiceWorker
{
    public class Worker : BackgroundService
	{
		private readonly ILogger<Worker> _logger;
		private readonly IBus _bus;

		//Important: The name of the Activity should be the same as the name of the Source added in the Web API startup AddOpenTelemetryTracing Builder
		private static readonly ActivitySource Activity = new("APITracing");
		private static readonly TextMapPropagator Propagator = new TraceContextPropagator();

		public Worker(ILogger<Worker> logger)
		{
			_logger = logger;
			_bus = RabbitMQFactory.CreateBus(BusType.DockerNetworkHost);
		}

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
			await _bus.ReceiveAsync<LocationRequest>(QueueType.Processing, (message, args) =>
			{
				Task.Run(() => { ProcessMessage(message, args); }, cancellationToken);
			});
        }

        private void ProcessMessage(LocationRequest message, BasicDeliverEventArgs args)
        {
			var parentContext = Propagator.Extract(default, args.BasicProperties, ExtractTraceContextFromBasicProperties);
			Baggage.Current = parentContext.Baggage;

			using (var activity = Activity.StartActivity("Process Message", ActivityKind.Consumer, parentContext.ActivityContext))
			{
                AddActivityTags(activity);
                _logger.LogInformation($"Message received location: {message.Latitude} - {message.Longitude}");
			}
		}

        private IEnumerable<string> ExtractTraceContextFromBasicProperties(IBasicProperties props, string key)
        {
            try
            {
                if (props.Headers.TryGetValue(key, out var value))
                {
                    var bytes = value as byte[];
                    return new[] { Encoding.UTF8.GetString(bytes) };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to extract trace context: {ex}");
            }

            return Enumerable.Empty<string>();
        }

        private void AddActivityTags(Activity activity)
        {
            activity?.SetTag("messaging.system", "rabbitmq");
            activity?.SetTag("messaging.destination_kind", "queue");
            activity?.SetTag("messaging.rabbitmq.queue", "sample"); //TODO: Glenn - Queue name?
            activity?.SetTag("messaging.destination", string.Empty);
            activity?.SetTag("messaging.rabbitmq.routing_key", QueueType.Processing);
        }
    }
}