using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using otel.Models;
using otel.QueueCommon;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace otel.ServiceWorker;

public class Worker: BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IBus _rabbitMQBus;

    private static readonly ActivitySource _activitySource = new("Aspire.RabbitMQ.Client");
    private static readonly TextMapPropagator Propagator = new TraceContextPropagator();

    private readonly IServiceProvider _serviceProvider;
    private IConnection? _messageConnection;
    private IModel? _messageChannel;

    public Worker(ILogger<Worker> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        // _rabbitMQBus = bus;
        _serviceProvider = serviceProvider;
    }

    // protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    // {
    //     _logger.LogInformation($"Awaiting messages...");

    //     await _rabbitMQBus.ReceiveAsync<Cart>(Queue.Orders, (message, args) =>
    //     {
    //         _logger.LogInformation($"Message received location: {message.Id}");

    //         Task.Run(() => { ProcessMessage(message, args); }, cancellationToken);
    //     });
    // }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Factory.StartNew(() =>
        {
             _logger.LogInformation($"Awaiting messages...");

            string queueName = Queue.Orders;

            _messageConnection = _serviceProvider.GetRequiredService<IConnection>();
            _messageChannel = _messageConnection.CreateModel();
            _messageChannel.QueueDeclare(queueName, true, false, false);

            var consumer = new EventingBasicConsumer(_messageChannel);
            consumer.Received += ProcessMessageAsync;

            _messageChannel.BasicConsume(queue: queueName,
                                         autoAck: true,
                                         consumer: consumer);
        }, stoppingToken, TaskCreationOptions.LongRunning, TaskScheduler.Current);
    }

    private void ProcessMessageAsync(object? sender, BasicDeliverEventArgs args)
    {
        var parentContext = Propagator.Extract(default, args.BasicProperties, ExtractTraceContextFromBasicProperties);
        Baggage.Current = parentContext.Baggage;

        using var activity = _activitySource.StartActivity($"{Queue.Orders} receive", ActivityKind.Consumer, parentContext.ActivityContext);
        AddActivityTags(activity);
        _logger.LogInformation($"Processing Order at: {DateTime.UtcNow}");

        var jsonSpecified = Encoding.UTF8.GetString(args.Body.Span);
        var item = JsonSerializer.Deserialize<Cart>(jsonSpecified);

        _logger.LogInformation($"Message received: {item.Id}");
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
        activity?.SetTag("messaging.rabbitmq.routing_key", Queue.Orders);
    }
}