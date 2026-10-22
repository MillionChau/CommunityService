using Community.Domain.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace Community.Domain.Entities
{
    /// <summary>
    /// Lưu trữ snapshot lịch sử bảng xếp hạng theo chu kỳ tuần hoặc tháng (SPEC-REP-01).
    /// </summary>
    [Table("LeaderboardSnapshots")]
    public class LeaderboardSnapshot : Entity<Guid>
    {
        public LeaderboardSnapshot()
        {
            Id = Guid.NewGuid();
            ClosedAt = DateTime.UtcNow;
            Entries = new List<LeaderboardEntry>();
        }

        public string PeriodType { get; set; } = "WEEKLY"; // WEEKLY hoặc MONTHLY
        public string PeriodCode { get; set; } = string.Empty; // e.g. "2026-W42" hoặc "2026-M10"
        public DateTime ClosedAt { get; set; } = DateTime.UtcNow;
        public virtual ICollection<LeaderboardEntry> Entries { get; set; }
    }

    [Table("LeaderboardEntries")]
    public class LeaderboardEntry : Entity<Guid>
    {
        public LeaderboardEntry()
        {
            Id = Guid.NewGuid();
        }

        public Guid SnapshotId { get; set; }
        public virtual LeaderboardSnapshot? Snapshot { get; set; }
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public int Rank { get; set; }
        public int Score { get; set; }
        public int PostsCount { get; set; }
        public int LikesCount { get; set; }
        public string TierTitle { get; set; } = string.Empty;
    }
}
