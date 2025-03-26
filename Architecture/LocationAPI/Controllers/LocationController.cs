using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using LocationAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using QueueCommon.Models;
using RabbitMQ.Client;

namespace LocationAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class LocationController : ControllerBase
    {
        private readonly ILogger<LocationController> _logger;
        private readonly IConnection _connection;

        //Important: The name of the Activity should be the same as the name of the Source added in the Web API startup AddOpenTelemetryTracing Builder
        private static readonly ActivitySource Activity = new("APITracing");
        private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

        public LocationController(ILogger<LocationController> logger, IConnection connection)
        {
            _logger = logger;
            _connection = connection;
        }

        [HttpGet]
        public async Task<Location> Get(double latitude, double longitude)
        {
            // https://www.rabbitmq.com/client-libraries/dotnet-api-guide#connection-and-channel-lifespan
            using var messageChannel = await _connection.CreateChannelAsync();
            await messageChannel.QueueDeclareAsync(QueueType.Processing, durable: true, exclusive: false, autoDelete: false);
            using var activity = Activity.StartActivity("RabbitMq Publish", ActivityKind.Producer);

            var properties = new BasicProperties();
            properties.Persistent = true;
            AddActivityToHeader(activity, properties);

            var output = JsonSerializer.Serialize(new LocationRequest()
            {
                Latitude = latitude,
                Longitude = longitude
            });

            await messageChannel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: QueueType.Processing,
                mandatory: true,
                basicProperties: properties,
                body: Encoding.UTF8.GetBytes(output));

            return new Location()
            {
                Name = "Antwerp",
                Latitude = latitude,
                Longitude = longitude
            };
        }

        private void AddActivityToHeader(Activity activity, IBasicProperties props)
        {
            try
            {
                Propagator.Inject(new PropagationContext(activity.Context, Baggage.Current), props, InjectContextIntoHeader);
                activity?.SetTag("messaging.system", "rabbitmq");
                activity?.SetTag("messaging.destination_kind", "queue");
                activity?.SetTag("messaging.rabbitmq.queue", "sample"); //TODO: Glenn - Queue name?
                activity?.SetTag("messaging.destination", string.Empty);
                activity?.SetTag("messaging.rabbitmq.routing_key", QueueType.Processing);
            }
            catch(Exception ex)
            {
                var t = ex.Message;
            }
        }

        private void InjectContextIntoHeader(IBasicProperties props, string key, string value)
        {
            try
            {
                props.Headers ??= new Dictionary<string, object>();
                props.Headers[key] = value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to inject trace context");
            }
        }
    }
}