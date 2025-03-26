﻿using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QueueCommon;
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
using System.Text.Json;

namespace ServiceWorker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private IConnection _connection;
        private IChannel _channel;

        //Important: The name of the Activity should be the same as the name of the Source added in the Web API startup AddOpenTelemetryTracing Builder
        private static readonly ActivitySource Activity = new("APITracing");
        private static readonly TextMapPropagator Propagator = new TraceContextPropagator();

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }
        
        private async Task InitializeAsync()
        {
            if (_connection is null)
                _connection = await RabbitMQFactory.CreateConnection(BusType.DockerNetworkHost, "app:opentelemetrygrafana component:event-consumer");
            if (_channel is null)
                _channel = await _connection.CreateChannelAsync();
        }

        protected override Task ExecuteAsync(CancellationToken cancellationToken)
        {
            return Task.Factory.StartNew(async () =>
            {
                await InitializeAsync();

                _logger.LogInformation($"Awaiting messages...");

                string queueName = QueueType.Processing;

                await _channel.QueueDeclareAsync(queueName, durable: true, exclusive: false, autoDelete: false);

                var consumer = new AsyncEventingBasicConsumer(_channel);
                consumer.ReceivedAsync += async (sender, args) =>
                {
                    await ProcessMessage(sender, args);
                };

                await _channel.BasicConsumeAsync(queue: queueName,
                                            autoAck: true,
                                            consumer: consumer);
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current);
        }

        override public async Task StopAsync(CancellationToken cancellationToken)
        {
            await base.StopAsync(cancellationToken);

            await _channel.DisposeAsync();
            await _connection.DisposeAsync();
        }

        private async Task ProcessMessage(object? sender, BasicDeliverEventArgs args)
        {
            var parentContext = Propagator.Extract(default, args.BasicProperties, ExtractTraceContextFromBasicProperties);
            Baggage.Current = parentContext.Baggage;

            using (var activity = Activity.StartActivity("Process Message", ActivityKind.Consumer, parentContext.ActivityContext))
            {
                AddActivityTags(activity);

                var jsonSpecified = Encoding.UTF8.GetString(args.Body.Span);
                var message = JsonSerializer.Deserialize<LocationRequest>(jsonSpecified);
                
                if (message is not null)
                {
                    _logger.LogInformation($"Message received location: {message.Latitude} - {message.Longitude}");
                }
            }
        }

        private IEnumerable<string> ExtractTraceContextFromBasicProperties(IReadOnlyBasicProperties props, string key)
        {
            try
            {
                if (props.Headers != null && 
                    props.Headers.TryGetValue(key, out var value) && 
                    value is byte[] bytes)
                {
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