using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace QueueCommon.Models.Interfaces
{
    public interface IBus
	{
		IBasicProperties GetBasicProperties();

		Task SendAsync<T>(string queue, T message, IBasicProperties basicProperties = null);

		Task ReceiveAsync<T>(string queue, Action<T, BasicDeliverEventArgs> onMessage);
	}
}