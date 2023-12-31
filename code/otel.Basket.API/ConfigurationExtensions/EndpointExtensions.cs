using otel.Models;

namespace otel.Basket.API;

public static class EndpointExtensions
{
    public static WebApplication MapEndpoints(this WebApplication app)    
    {
        var catalogService = app.Services.GetRequiredService<ICatalogService>();

        app.MapPost("/carts", () =>
        {
            Cart cart = new Cart(Guid.NewGuid());
            return Results.Created($"/carts/{cart.Id}", cart);
        });

        app.MapPost("/carts/{cartId}/items", async (Guid cartId, CartItem item) =>
        {
            var product = await catalogService.GetProduct(item.ProductId);
            return Results.Created($"/carts/{cartId}/items", item);
        });

        return app;
    }
}