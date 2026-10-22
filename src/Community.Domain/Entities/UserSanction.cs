using Community.Domain.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace Community.Domain.Entities
{
    /// <summary>
    /// Bản ghi xử phạt và hạn chế quyền người dùng do điểm tín nhiệm quá thấp (SPEC-REP-01).
    /// </summary>
    [Table("UserSanctions")]
    public class UserSanction : Entity<Guid>
    {
        public UserSanction()
        {
            Id = Guid.NewGuid();
            StartedAt = DateTime.UtcNow;
            IsActive = true;
        }

        public Guid UserId { get; set; }
        /// <summary>
        /// 1: Warning, 2: Sandbox (Duyệt trước), 3: Mute (Chỉ đọc), 4: Banned (Đình chỉ)
        /// </summary>
        public int SanctionLevel { get; set; }
        public string Reason { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; }
        public bool IsActive { get; set; } = true;
        public string? IssuedBy { get; set; } // "SYSTEM" hoặc Admin/Moderator User ID
    }
}
