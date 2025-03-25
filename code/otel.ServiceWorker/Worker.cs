using System.Diagnostics;
using System.Text;
using System.Text.Json;
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
    private static readonly ActivitySource _activitySource = new("Aspire.RabbitMQ.Client");
    private static readonly TextMapPropagator Propagator = new TraceContextPropagator();
    private readonly IServiceProvider _serviceProvider;
    private readonly ICatalogService _catalogService;
    private IConnection? _messageConnection;
    private IChannel? _messageChannel;

    public Worker(ILogger<Worker> logger, IServiceProvider serviceProvider, ICatalogService catalogService)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _catalogService = catalogService;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Factory.StartNew(async () =>
        {
             _logger.LogInformation($"Awaiting messages...");

            string queueName = Queue.Orders;

            _messageConnection = _serviceProvider.GetRequiredService<IConnection>();
            _messageChannel = await _messageConnection.CreateChannelAsync();
            await _messageChannel.QueueDeclareAsync(queueName, durable: true, exclusive: false, autoDelete: false);

            var consumer = new AsyncEventingBasicConsumer(_messageChannel);
            consumer.ReceivedAsync += async (s, e) => await ProcessMessageAsync(s, e);

            await _messageChannel.BasicConsumeAsync(queue: queueName,
                                         autoAck: true,
                                         consumer: consumer);
        }, stoppingToken, TaskCreationOptions.LongRunning, TaskScheduler.Current);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        _messageChannel?.Dispose();
        _messageConnection?.Dispose();
    }

    private async Task ProcessMessageAsync(object? sender, BasicDeliverEventArgs args)
    {
        var parentContext = Propagator.Extract(default, args.BasicProperties, ExtractTraceContextFromBasicProperties);
        Baggage.Current = parentContext.Baggage;

        using var activity = _activitySource.StartActivity($"{Queue.Orders} receive", ActivityKind.Consumer, parentContext.ActivityContext);
        if (activity is not null)
        {
            AddActivityTags(activity);
            _logger.LogInformation($"Processing Order at: {DateTime.UtcNow}");

            var jsonSpecified = Encoding.UTF8.GetString(args.Body.Span);
            var item = JsonSerializer.Deserialize<Cart>(jsonSpecified);
            
            if (item is not null)
            {
                _logger.LogInformation($"Message received: {item.Id}");
                _logger.LogInformation($"Message received: {jsonSpecified}");

                List<Task> tasks = new List<Task>();            
                foreach (var cartItem in item.Items)
                {
                    tasks.Add(Task.Run(() => _catalogService.GetProduct(cartItem.Id)));
                }

                await Task.WhenAll(tasks);
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
        activity?.SetTag("messaging.rabbitmq.queue", "sample");
        activity?.SetTag("messaging.destination", string.Empty);
        activity?.SetTag("messaging.rabbitmq.routing_key", Queue.Orders);
    }
}