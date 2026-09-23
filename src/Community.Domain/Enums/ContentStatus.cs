namespace Community.Domain.Enums;

/// <summary>
/// Vòng đời nội dung (Post / Comment) theo SRS DevRadar:
/// Draft → Published → (Hidden khi bị Admin ẩn do vi phạm) → Deleted (soft delete).
/// Lưu xuống DB dạng int để tương thích ngược với dữ liệu cũ.
/// </summary>
public enum ContentStatus
{
    Draft = 0,
    Published = 1,
    Hidden = 2,
    Deleted = 3
}
