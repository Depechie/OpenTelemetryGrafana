using System;
using System.Text;
using System.Text.Json;
using QueueCommon.Models.Interfaces;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace QueueCommon.Models
{
	public class RabbitMQBus : IBus
	{
        private IModel _channel;

        public RabbitMQBus(IModel channel)
		{
            _channel = channel;
		}

        public IBasicProperties GetBasicProperties() => _channel.CreateBasicProperties();

        public async Task ReceiveAsync<T>(string queue, Action<T, BasicDeliverEventArgs> onMessage)
        {
            _channel.QueueDeclare(
                queue: queue,
                durable: true,
                exclusive: false,
                autoDelete: false
            );

            var consumer = new AsyncEventingBasicConsumer(_channel);

            consumer.Received += async (s, e) =>
            {
                var jsonSpecified = Encoding.UTF8.GetString(e.Body.Span);
                var item = JsonSerializer.Deserialize<T>(jsonSpecified);

                onMessage(item, e);

                await Task.Yield();
            };

            _channel.BasicConsume(queue, true, consumer);

            await Task.Yield();
        }

        public async Task SendAsync<T>(string queue, T message, IBasicProperties basicProperties = null)
        {
            await Task.Run(() =>
            {
                _channel.QueueDeclare(queue, true, false, false);

                var properties = basicProperties ?? _channel.CreateBasicProperties();
                properties.Persistent = false;

                var output = JsonSerializer.Serialize(message);

                _channel.BasicPublish(string.Empty, queue, properties, Encoding.UTF8.GetBytes(output));
            });
        }

        public Task SendAsync<T>(string queue, T message)
        {
            throw new NotImplementedException();
        }
    }
}

