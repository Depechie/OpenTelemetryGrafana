using System.Text.Json;
using otel.Models;
using otel.QueueCommon;
using RabbitMQ.Client;

namespace otel.Basket.API;

public static class EndpointExtensions
{
    public static WebApplication MapEndpoints(this WebApplication app)    
    {
        var catalogService = app.Services.GetRequiredService<ICatalogService>();
        var carts = new Dictionary<Guid, Cart>();
        var rabbitMQBus = app.Services.GetService<IBus>();

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

            if (rabbitMQBus is null)
                return Results.StatusCode(503);
            else
                await rabbitMQBus.SendAsync(Queue.Orders, cart);

            return Results.Created($"/carts/{cartId}/checkout", cart);
        });

        return app;
    }
}