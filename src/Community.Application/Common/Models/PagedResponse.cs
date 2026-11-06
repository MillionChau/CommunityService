namespace Community.Application.Common.Models;

/// <summary>
/// Kết quả phân trang chuẩn cho các query danh sách (SRS FR-10: xem bảng tin có phân trang).
/// Hỗ trợ cả Page-based pagination và Cursor-based pagination cho Infinite Scroll.
/// </summary>
public class PagedResponse<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public string? NextCursor { get; set; }
    public bool HasMore { get; set; }

    public static PagedResponse<T> Create(
        IReadOnlyList<T> items,
        int page,
        int pageSize,
        int totalCount,
        string? nextCursor = null,
        bool hasMore = false)
        => new()
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            NextCursor = nextCursor,
            HasMore = hasMore
        };
}
