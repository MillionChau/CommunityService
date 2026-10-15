using Community.Domain.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace Community.Domain.Entities
{
    /// <summary>
    /// Quan hệ theo dõi giữa 2 người dùng (FR-30 / UC-30).
    /// FollowerId: người ấn theo dõi
    /// FolloweeId: tác giả được theo dõi
    /// </summary>
    [Table("UserFollows")]
    public class UserFollow : AuditableEntity<Guid>
    {
        public Guid FollowerId { get; set; }
        public Guid FolloweeId { get; set; }
    }
}
