using System.Diagnostics;
using System.Text;
using System.Text.Json;
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

    public static async Task<Results<Created<Cart>, NotFound, StatusCodeHttpResult>> Checkout(IConnection messageConnection, Guid cartId)
    {
        if (!_carts.TryGetValue(cartId, out Cart cart))
            return TypedResults.NotFound();

        // https://www.rabbitmq.com/client-libraries/dotnet-api-guide#connection-and-channel-lifespan
        using var messageChannel = await messageConnection.CreateChannelAsync();
        await messageChannel.QueueDeclareAsync(Queue.Orders, durable: true, exclusive: false, autoDelete: false);

        using var activity = _activitySource.StartActivity($"{Queue.Orders} publish", ActivityKind.Producer);
        var properties = new BasicProperties();
        properties.Persistent = true;
        AddActivityToHeader(activity, properties);

        var output = JsonSerializer.Serialize(cart);

        await messageChannel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: Queue.Orders,
            mandatory: true,
            basicProperties: properties,
            body: Encoding.UTF8.GetBytes(output));

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