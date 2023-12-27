namespace otel.Models;

public class Product
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? ImageURL { get; set; }
    public string? HoverImageURL { get; set; }
    public string? ThumbnailImageURL { get; set; }
    public decimal? Price { get; set; }
    public int Rating { get; set; }
}