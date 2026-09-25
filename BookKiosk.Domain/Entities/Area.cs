namespace BookKiosk.Domain.Entities;

public class Area : BaseEntity
{
    public int AreaId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? MapCoordinates { get; set; }

    // Navigation Property
    public ICollection<Book> Books { get; set; } = new List<Book>();
}
