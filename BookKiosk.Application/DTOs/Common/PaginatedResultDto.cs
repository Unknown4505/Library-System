namespace BookKiosk.Application.DTOs.Common;

/// <summary>
/// Wrapper phân trang chuẩn — dùng cho GET /api/books?page=1&amp;pageSize=12&amp;keyword=...
/// </summary>
public class PaginatedResultDto<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }

    /// <summary>
    /// Tổng số trang, tính sẵn để FE không cần tự tính.
    /// Trả về 0 nếu không có item hoặc PageSize không hợp lệ.
    /// </summary>
    public int TotalPages => PageSize > 0
        ? (int)Math.Ceiling((double)TotalCount / PageSize)
        : 0;

    /// <summary>Còn trang tiếp theo không (dùng cho nút "Load More")</summary>
    public bool HasNextPage => Page > 0 && Page < TotalPages;
}

