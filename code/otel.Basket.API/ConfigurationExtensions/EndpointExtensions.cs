using System.Text.Json;
using otel.Models;
using RabbitMQ.Client;

namespace otel.Basket.API;

public static class EndpointExtensions
{
    public static WebApplication MapEndpoints(this WebApplication app)    
    {
        var catalogService = app.Services.GetRequiredService<ICatalogService>();
        var carts = new Dictionary<Guid, Cart>();
        var messageConnection = app.Services.GetService<IConnection>();

        app.MapGet("/carts", () => Results.Ok(carts.Values));

        app.MapPost("/carts", () =>
        {
            Cart cart = new Cart(Guid.NewGuid());
            carts.Add(cart.Id, cart);

            return Results.Created($"/carts/{cart.Id}", cart);
        });

        app.MapPost("/carts/{cartId}/items", async (Guid cartId, CartItem item) =>
        {
            var product = await catalogService.GetProduct(item.ProductId);
            if (carts.TryGetValue(cartId, out Cart cart))
                cart.Items.Add(product);
            
            return Results.Created($"/carts/{cartId}/items", item);
        });

        app.MapPost("/carts/{cartId}/checkout", async (Guid cartId) =>
        {
            if (!carts.TryGetValue(cartId, out Cart cart))
                return Results.NotFound();

            // If the connection is null return a 503
            if (messageConnection is null)
                return Results.StatusCode(503);
            else
            {
                const string configKeyName = "Aspire:RabbitMQ:Client:OrderQueueName";
                string? queueName = app.Configuration[configKeyName];
                if (string.IsNullOrEmpty(queueName))
                    return Results.StatusCode(503);

                using var channel = messageConnection.CreateModel();
                channel.QueueDeclare(queueName, exclusive: false);
                channel.BasicPublish(
                    exchange: "",
                    routingKey: queueName,
                    basicProperties: null,
                    body: JsonSerializer.SerializeToUtf8Bytes(cart));
            }

            return Results.Created($"/carts/{cartId}/checkout", cart);
        });

        return app;
    }
}