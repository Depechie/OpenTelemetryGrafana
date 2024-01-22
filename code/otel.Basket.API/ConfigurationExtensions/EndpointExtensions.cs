using System.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using otel.Models;
using otel.QueueCommon;
using RabbitMQ.Client;

namespace otel.Basket.API;

public static class EndpointExtensions
{
    private static Dictionary<Guid, Cart> _carts = new Dictionary<Guid, Cart>();
    private static readonly ActivitySource _activitySource = new("Aspire.RabbitMQ.Client");
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

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
        {
            using var activity = _activitySource.StartActivity($"{Queue.Orders} publish", ActivityKind.Producer);
            var basicProperties = rabbitMQBus.GetBasicProperties();
            AddActivityToHeader(activity, basicProperties);            
            await rabbitMQBus.SendAsync(Queue.Orders, cart, basicProperties);
        }

        // _carts.Remove(cartId);

        return TypedResults.Created($"/carts/{cartId}/checkout", cart);
    }

    private static void AddActivityToHeader(Activity activity, IBasicProperties props)
    {
        try
        {
            Propagator.Inject(new PropagationContext(activity.Context, Baggage.Current), props, InjectContextIntoHeader);
            activity?.SetTag("messaging.system", "rabbitmq");
            activity?.SetTag("messaging.destination_kind", "queue");
            activity?.SetTag("messaging.rabbitmq.queue", "sample"); //TODO: Glenn - Queue name?
            activity?.SetTag("messaging.destination", string.Empty);
            activity?.SetTag("messaging.rabbitmq.routing_key", Queue.Orders);
        }
        catch(Exception ex)
        {
            var t = ex.Message;
        }
    }

    private static void InjectContextIntoHeader(IBasicProperties props, string key, string value)
    {
        try
        {
            props.Headers ??= new Dictionary<string, object>();
            props.Headers[key] = value;
        }
        catch (Exception ex)
        {
            // _logger.LogError(ex, "Failed to inject trace context");
        }
    }    
}