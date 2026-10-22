using Community.Domain.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace Community.Domain.Entities
{
    /// <summary>
    /// Sổ cái ghi log chi tiết từng biến động điểm uy tín và tín nhiệm (Audit Log).
    /// </summary>
    [Table("ReputationTransactions")]
    public class ReputationTransaction : Entity<Guid>
    {
        public ReputationTransaction()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
        }

        public Guid UserId { get; set; }
        public virtual UserReputation? UserReputation { get; set; }

        public string ActionType { get; set; } = string.Empty;
        // POST_CREATED, LIKE_RECEIVED, BOOKMARK_RECEIVED, COMMENT_CREATED, COMMENT_LIKE, 
        // BEST_SOLUTION, AI_QUALITY_BONUS, TOXIC_PENALTY, REPORT_PENALTY, INACTIVITY_DECAY,
        // PASSIVE_RECOVERY, ACTIVE_RECOVERY, APPEAL_REVERSAL

        public int PointsChanged { get; set; } // Ví dụ: +20, -30
        public int TssChanged { get; set; }    // Ví dụ: 0, -15, +3, +5
        public Guid? SourceId { get; set; }    // PostId hoặc CommentId
        public string? Reason { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
