namespace BookKiosk.Application.DTOs.Books;

/// <summary>
/// DTO trả về thông tin danh mục sách (dùng cho dropdown/filter ở Kiosk và CMS)
/// </summary>
public class CategoryDto
{
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
