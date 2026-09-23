namespace Community.Application.Common.Models;

/// <summary>
/// Kết quả phân trang chuẩn cho các query danh sách (SRS FR-10: xem bảng tin có phân trang).
/// </summary>
public class PagedResponse<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public static PagedResponse<T> Create(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
        => new() { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
}
