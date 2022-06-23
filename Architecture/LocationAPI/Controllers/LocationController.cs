using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using LocationAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using QueueCommon.Models;
using QueueCommon.Models.Interfaces;
using RabbitMQ.Client;

namespace LocationAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class LocationController : ControllerBase
    {
        private readonly ILogger<LocationController> _logger;
        private readonly IBus _bus;

        //Important: The name of the Activity should be the same as the name of the Source added in the Web API startup AddOpenTelemetryTracing Builder
        private static readonly ActivitySource Activity = new("APITracing");
        private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

        public LocationController(ILogger<LocationController> logger, IBus bus)
        {
            _logger = logger;
            _bus = bus;
        }

        [HttpGet]
        public async Task<Location> Get(double latitude, double longitude)
        {
            using (var activity = Activity.StartActivity("RabbitMq Publish", ActivityKind.Producer))
            {
                var basicProperties = _bus.GetBasicProperties();
                AddActivityToHeader(activity, basicProperties);

                await _bus.SendAsync(QueueType.Processing, new LocationRequest()
                {
                    Latitude = latitude,
                    Longitude = longitude
                }, basicProperties);
            }

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