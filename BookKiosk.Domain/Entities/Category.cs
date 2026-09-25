namespace BookKiosk.Domain.Entities;

public class Category : BaseEntity
{
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Navigation Property
    public ICollection<Book> Books { get; set; } = new List<Book>();
}
