namespace otel.Models;

public class Cart
{
    public Guid Id { get; set; }
    public List<Product> Items { get; set; } = new List<Product>();

    public Cart(Guid id)
    {
        Id = id;
    }
}
