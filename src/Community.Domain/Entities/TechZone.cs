using Community.Domain.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace Community.Domain.Entities;

/// <summary>
/// Chủ đề công nghệ / TechZone (UC-14 & UC-69 → UC-74).
/// </summary>
[Table("TechZones")]
public class TechZone : AuditableEntity<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconUrl { get; set; }
    public string? ColorCode { get; set; }
    public string? Tags { get; set; } // Ví dụ: "dotnet,csharp,aspnet,efcore"
    public int DisplayOrder { get; set; } = 0;
    public int Status { get; set; } = 1; // 1 = Active, 0 = Inactive
    public int PostsCount { get; set; } = 0;
    public int FollowersCount { get; set; } = 0;

    public virtual ICollection<Post>? Posts { get; set; }
}
