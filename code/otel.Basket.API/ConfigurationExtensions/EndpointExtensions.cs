using Microsoft.AspNetCore.Http.HttpResults;
using otel.Models;
using otel.QueueCommon;

namespace otel.Basket.API;

public static class EndpointExtensions
{
    private static Dictionary<Guid, Cart> _carts = new Dictionary<Guid, Cart>();

    public static IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder app)
    {
        // var rabbitMQBus = app.Services.GetService<IBus>();

        app.MapGet("/carts", GetCarts);
        app.MapPost("/carts", CreateCart);
        app.MapPost("/carts/{cartId}/items", AddItemToCart);
        app.MapPost("/carts/{cartId}/checkout", Checkout);

        return app;
    }

    public static async Task<Results<Ok<Dictionary<Guid, Cart>.ValueCollection>, BadRequest<string>>> GetCarts() => await Task.FromResult(TypedResults.Ok(_carts.Values));

    public static async Task<Created<Cart>> CreateCart()
    {
        Cart cart = new Cart(Guid.NewGuid());
        _carts.Add(cart.Id, cart);

        return TypedResults.Created($"/carts/{cart.Id}", cart);
    }

    public static async Task<Results<Created<CartItem>, NotFound>> AddItemToCart(ICatalogService catalogService, Guid cartId, CartItem item)
    {
        if (!_carts.TryGetValue(cartId, out Cart cart))
            return TypedResults.NotFound();
            
        var product = await catalogService.GetProduct(item.ProductId);
        cart.Items.Add(product);
        
        return TypedResults.Created($"/carts/{cartId}/items", item);
    }

    public static async Task<Results<Created<Cart>, NotFound, StatusCodeHttpResult>> Checkout(IBus rabbitMQBus, Guid cartId)
    {
        if (!_carts.TryGetValue(cartId, out Cart cart))
            return TypedResults.NotFound();

        if (rabbitMQBus is null)
            return TypedResults.StatusCode(503);
        else
            await rabbitMQBus.SendAsync(Queue.Orders, cart);

        // _carts.Remove(cartId);

        return TypedResults.Created($"/carts/{cartId}/checkout", cart);
    }
}