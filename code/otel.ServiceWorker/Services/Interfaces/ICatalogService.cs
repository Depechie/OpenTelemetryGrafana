using otel.Models;

namespace otel.ServiceWorker;

public interface ICatalogService
{
    Task<Product> GetProduct(Guid id);
}
