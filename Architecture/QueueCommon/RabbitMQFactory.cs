using QueueCommon.Models;
using RabbitMQ.Client;

namespace QueueCommon;

public class RabbitMQFactory
{
    private static ConnectionFactory _factory;
    private static IConnection _connection;

    public static async Task<IConnection> CreateConnection(string hostName, string ClientProviderName)
    {
        _factory = new ConnectionFactory() { HostName = hostName, ClientProvidedName = ClientProviderName };
        _connection = await _factory.CreateConnectionAsync();

        return _connection;
    }
}