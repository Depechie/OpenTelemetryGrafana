using otel.Models;

namespace otel.Basket.API;

public interface ICatalogService
{
    Task<Product> GetProduct(Guid id);
}
