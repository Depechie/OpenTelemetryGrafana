using System.Text.Json;
using otel.Models;

namespace otel.ServiceWorker;

public class CatalogService : ICatalogService
{
    private HttpClient _httpClient;
    private JsonSerializerOptions _options;

    public CatalogService(HttpClient httpClient)
    {
        _httpClient = httpClient;

        _options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<Product> GetProduct(Guid id)
    {
        var response = await _httpClient.GetStringAsync($"http://localhost:5251/items/{id}");
        return JsonSerializer.Deserialize<Product>(response, _options);
    }
}
