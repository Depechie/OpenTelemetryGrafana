using otel.Models;

namespace otel.Catalog.API;

public static class EndpointExtensions
{
    public static WebApplication MapEndpoints(this WebApplication app)
    {
        var products = new List<Product>();
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
            products.Add(product);
        }
                
        app.MapGet("/items", () =>
        {
            return products;
        })
        .WithName("GetItems")
        .WithOpenApi();

        app.MapGet("/items/{id}", (Guid id) =>
        {
            var product = products.FirstOrDefault(p => p.Id == id);
            return Results.Ok(product);
        })
        .WithName("GetItem")
        .WithOpenApi();
        
        return app;
    }
}
