using Microsoft.AspNetCore.Http.HttpResults;
using otel.Models;

namespace otel.Catalog.API;

public static class EndpointExtensions
{
    private static List<Product> _products = new List<Product>();

    public static IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder app)
    {
        initData();

        app.MapGet("/items", GetItems);
        app.MapGet("/items/{id:Guid}", GetItem);

        return app;
    }

    private static void initData()
    {
        var random = new Random();

        for (int i = 0; i < 5; i++)
        {
            var id = Guid.NewGuid();
            var name = $"Product {i + 1}";
            var price = (decimal)(random.NextDouble() * 100);

            var product = new Product()
            {
                Id = id,
                Name = name,
                Price = price
            };
            _products.Add(product);
        }
    }

    public static async Task<Results<Ok<List<Product>>, BadRequest<string>>> GetItems() => await Task.FromResult(TypedResults.Ok(_products));

    public static async Task<Results<Ok<Product>, NotFound, BadRequest<string>>> GetItem(Guid id)
    {
        var product = _products.FirstOrDefault(p => p.Id == id);
        return await Task.FromResult(TypedResults.Ok(product));
    }
}
