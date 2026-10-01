namespace BookKiosk.Application.DTOs.Books;

/// <summary>
/// DTO trả về thông tin khu vực kệ sách (dùng để hiển thị vị trí kệ ở BookDetailPage)
/// </summary>
public class AreaDto
{
    public int AreaId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Tọa độ sơ đồ kệ — hiển thị bản đồ mặt bằng ở BookDetailPage</summary>
    public string? MapCoordinates { get; set; }
}

