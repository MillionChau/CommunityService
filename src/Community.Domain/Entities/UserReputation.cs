using Community.Domain.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Community.Domain.Entities
{
    /// <summary>
    /// Điểm uy tín, cấp bậc và tín nhiệm người dùng (SPEC-REP-01).
    /// </summary>
    [Table("UserReputations")]
    public class UserReputation : Entity<Guid>
    {
        public UserReputation()
        {
            Id = Guid.NewGuid();
            TrustSafetyScore = 80;
            CurrentTierLevel = 1;
            TierTitle = "Code Initiate";
            LastActivityAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public Guid UserId { get; set; }
        public string? Username { get; set; }
        public string? AvatarUrl { get; set; }

        /// <summary>
        /// Điểm Uy tín Vĩnh viễn (Permanent Reputation Points - PRP).
        /// Tích lũy trọn đời xác định Cây Cấp bậc (Tier).
        /// </summary>
        public int PermanentReputationPoints { get; set; } = 0;

        /// <summary>
        /// Điểm Tín nhiệm & An toàn (Trust & Safety Score - TSS, thang 0-100).
        /// </summary>
        [Range(0, 100)]
        public int TrustSafetyScore { get; set; } = 80;

        /// <summary>
        /// Cấp bậc hiện tại (1: Code Initiate -> 6: DevRadar Fellow).
        /// </summary>
        public int CurrentTierLevel { get; set; } = 1;
        public string TierTitle { get; set; } = "Code Initiate";

        /// <summary>
        /// Điểm Đóng góp Tuần (Weekly Contribution Score - PCS_W).
        /// </summary>
        public int WeeklyContributionScore { get; set; } = 0;

        /// <summary>
        /// Điểm Đóng góp Tháng (Monthly Contribution Score - PCS_M).
        /// </summary>
        public int MonthlyContributionScore { get; set; } = 0;

        public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastTssPassiveRecoveryAt { get; set; }
        public int ConsecutiveCleanWeeks { get; set; } = 0;
        public int ConsecutiveInactiveWeeks { get; set; } = 0;

        // Trạng thái Chế tài (Sanction)
        public bool IsSanctioned { get; set; } = false;
        public int SanctionLevel { get; set; } = 0; // 0: None, 1: Warning, 2: Sandbox, 3: Mute, 4: Banned
        public DateTime? SanctionExpiresAt { get; set; }
        public string? SanctionReason { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<ReputationTransaction>? Transactions { get; set; }
    }
}
