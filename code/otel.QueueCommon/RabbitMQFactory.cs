using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace otel.QueueCommon;

public class RabbitMQFactory
{
    private static IConnection _connection;
    private static IModel _channel;

    public static IBus CreateBus(IConnection connection)
    {
        _connection = connection;
        _channel = _connection.CreateModel();

        return new RabbitMQBus(_channel);
    }
}