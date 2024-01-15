using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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

            string queueName = "orders";

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
        _logger.LogInformation($"Processing Order at: {DateTime.UtcNow}");

        var jsonSpecified = Encoding.UTF8.GetString(args.Body.Span);
        var item = JsonSerializer.Deserialize<Cart>(jsonSpecified);

        _logger.LogInformation($"Message received: {item.Id}");
    }

    private void ProcessMessage(Cart message, BasicDeliverEventArgs args)
    {
        _logger.LogInformation($"Message received: {message.Id}");
    }
}