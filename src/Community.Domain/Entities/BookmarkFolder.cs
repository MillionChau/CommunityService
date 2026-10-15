using Community.Domain.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace Community.Domain.Entities;

/// <summary>
/// Thư mục / Danh mục lưu trữ bookmark cá nhân (FR-21 / UC-21).
/// </summary>
[Table("BookmarkFolders")]
public class BookmarkFolder : AuditableEntity<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid UserId { get; set; }
    public string? ColorCode { get; set; }
    public int ItemsCount { get; set; } = 0;

    public virtual ICollection<PostBookmark>? Bookmarks { get; set; }
}
