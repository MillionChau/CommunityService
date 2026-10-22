namespace Community.Application.DTOs.Reputation;

public class UserReputationDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Điểm Uy tín Vĩnh viễn (PRP)
    /// </summary>
    public int PermanentReputationPoints { get; set; }

    /// <summary>
    /// Điểm Tín nhiệm & An toàn (TSS, thang 0-100)
    /// </summary>
    public int TrustSafetyScore { get; set; }

    /// <summary>
    /// Cấp bậc hiện tại (1 - 6)
    /// </summary>
    public int CurrentTierLevel { get; set; }
    public string TierTitle { get; set; } = "Code Initiate";
    public string TierBadge { get; set; } = "🌱 Tân thủ";

    public string? NextTierTitle { get; set; }
    public int? NextTierRequiredPoints { get; set; }
    public int? PointsToNextTier { get; set; }
    public double ProgressPercentage { get; set; }

    /// <summary>
    /// Điểm đóng góp Tuần & Tháng
    /// </summary>
    public int WeeklyContributionScore { get; set; }
    public int MonthlyContributionScore { get; set; }

    // Trạng thái Chế tài & Quyền hạn
    public bool IsSanctioned { get; set; }
    public int SanctionLevel { get; set; }
    public string? SanctionReason { get; set; }
    public DateTime? SanctionExpiresAt { get; set; }

    public bool CanPost { get; set; } = true;
    public bool CanComment { get; set; } = true;
    public bool IsSandboxModerated { get; set; } = false;

    public List<string> Privileges { get; set; } = new();
}

public class ReputationTransactionDto
{
    public Guid Id { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string ActionDescription { get; set; } = string.Empty;
    public int PointsChanged { get; set; }
    public int TssChanged { get; set; }
    public Guid? SourceId { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SanctionRequestDto
{
    public Guid UserId { get; set; }
    public int SanctionLevel { get; set; } // 1: Warning, 2: Sandbox, 3: Mute, 4: Banned
    public int? DurationDays { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class PardonRequestDto
{
    public Guid UserId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool RestorePoints { get; set; } = true;
}
