using Community.Application.DTOs.Leaderboard;
using Community.Application.DTOs.Reputation;
using Community.Application.Interfaces;
using Community.Domain.Entities;
using Community.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Community.Infrastructure.Services;

public class ReputationService : IReputationService
{
    private readonly CommunityDbContext _db;
    private readonly ICacheService _cache;
    private readonly ILogger<ReputationService> _logger;

    public ReputationService(
        CommunityDbContext db,
        ICacheService cache,
        ILogger<ReputationService> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    private static (int level, string title, string badge, string? nextTitle, int? nextReq) CalculateTier(int prp)
    {
        if (prp >= 5000)
            return (6, "DevRadar Fellow", "🏆 Huyền thoại công nghệ", null, null);
        if (prp >= 2500)
            return (5, "Principal Architect", "👑 Kiến trúc sư trưởng", "DevRadar Fellow", 5000);
        if (prp >= 1000)
            return (4, "Tech Lead", "⭐ Trưởng nhóm kỹ thuật", "Principal Architect", 2500);
        if (prp >= 400)
            return (3, "Senior Contributor", "🚀 Đóng góp tích cực", "Tech Lead", 1000);
        if (prp >= 100)
            return (2, "Developer", "💻 Lập trình viên", "Senior Contributor", 400);

        return (1, "Code Initiate", "🌱 Tân thủ", "Developer", 100);
    }

    private async Task<UserReputation> GetOrCreateReputationAsync(Guid userId, CancellationToken ct)
    {
        var rep = await _db.UserReputations.FirstOrDefaultAsync(r => r.UserId == userId, ct);
        if (rep == null)
        {
            rep = new UserReputation
            {
                UserId = userId,
                Username = $"Dev_{userId.ToString()[..6]}",
                PermanentReputationPoints = 0,
                TrustSafetyScore = 80,
                CurrentTierLevel = 1,
                TierTitle = "Code Initiate",
                LastActivityAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.UserReputations.Add(rep);
            await _db.SaveChangesAsync(ct);
        }
        return rep;
    }

    public async Task<UserReputationDto> GetUserReputationAsync(Guid userId, CancellationToken ct = default)
    {
        var rep = await GetOrCreateReputationAsync(userId, ct);

        // Check if sanction expired
        if (rep.IsSanctioned && rep.SanctionExpiresAt.HasValue && rep.SanctionExpiresAt.Value < DateTime.UtcNow)
        {
            rep.IsSanctioned = false;
            rep.SanctionLevel = 0;
            rep.SanctionReason = null;
            rep.SanctionExpiresAt = null;
            await _db.SaveChangesAsync(ct);
        }

        var (level, title, badge, nextTitle, nextReq) = CalculateTier(rep.PermanentReputationPoints);
        if (rep.CurrentTierLevel != level || rep.TierTitle != title)
        {
            rep.CurrentTierLevel = level;
            rep.TierTitle = title;
            await _db.SaveChangesAsync(ct);
        }

        int? pointsToNext = nextReq.HasValue ? Math.Max(0, nextReq.Value - rep.PermanentReputationPoints) : null;
        double progress = 100.0;
        if (nextReq.HasValue)
        {
            var basePoints = level switch
            {
                1 => 0,
                2 => 100,
                3 => 400,
                4 => 1000,
                5 => 2500,
                _ => 0
            };
            var span = nextReq.Value - basePoints;
            var currentInSpan = rep.PermanentReputationPoints - basePoints;
            progress = span > 0 ? Math.Min(100.0, Math.Max(0.0, Math.Round((double)currentInSpan / span * 100, 1))) : 100.0;
        }

        var privileges = new List<string>();
        if (level >= 1) privileges.Add("Đọc mọi bài viết, lưu bài, theo dõi tác giả");
        if (level >= 2) privileges.Add("Tạo TechZone tags mới, bình luận tức thì");
        if (level >= 3) privileges.Add("Huy hiệu Bạc, không giới hạn bài viết/ngày, gắn cờ ưu tiên");
        if (level >= 4) privileges.Add("Huy hiệu Vàng, bài viết được gợi ý Trending, miễn kiểm duyệt trước");
        if (level >= 5) privileges.Add("Huy hiệu Kim cương, quyền đề xuất Ghim bài viết TechZone");
        if (level >= 6) privileges.Add("Khung Avatar phát sáng, tham gia Ban Cố vấn Kỹ thuật");

        // Calculate permissions based on TSS & Sanctions
        var canPost = rep.SanctionLevel < 3 && rep.TrustSafetyScore >= 20;
        var canComment = rep.SanctionLevel < 3 && rep.TrustSafetyScore >= 20;
        var isSandbox = rep.SanctionLevel == 2 || (rep.TrustSafetyScore >= 40 && rep.TrustSafetyScore < 60);

        return new UserReputationDto
        {
            UserId = rep.UserId,
            Username = rep.Username ?? $"Dev_{userId.ToString()[..6]}",
            AvatarUrl = rep.AvatarUrl,
            PermanentReputationPoints = rep.PermanentReputationPoints,
            TrustSafetyScore = rep.TrustSafetyScore,
            CurrentTierLevel = level,
            TierTitle = title,
            TierBadge = badge,
            NextTierTitle = nextTitle,
            NextTierRequiredPoints = nextReq,
            PointsToNextTier = pointsToNext,
            ProgressPercentage = progress,
            WeeklyContributionScore = rep.WeeklyContributionScore,
            MonthlyContributionScore = rep.MonthlyContributionScore,
            IsSanctioned = rep.IsSanctioned,
            SanctionLevel = rep.SanctionLevel,
            SanctionReason = rep.SanctionReason,
            SanctionExpiresAt = rep.SanctionExpiresAt,
            CanPost = canPost,
            CanComment = canComment,
            IsSandboxModerated = isSandbox,
            Privileges = privileges
        };
    }

    public async Task AddPointsAsync(
        Guid userId,
        string actionType,
        int pointsDelta,
        int tssDelta = 0,
        Guid? sourceId = null,
        string? reason = null,
        CancellationToken ct = default)
    {
        var rep = await GetOrCreateReputationAsync(userId, ct);

        rep.PermanentReputationPoints = Math.Max(0, rep.PermanentReputationPoints + pointsDelta);
        rep.WeeklyContributionScore += pointsDelta;
        rep.MonthlyContributionScore += pointsDelta;
        rep.LastActivityAt = DateTime.UtcNow;
        rep.ConsecutiveInactiveWeeks = 0;

        if (tssDelta > 0)
        {
            rep.TrustSafetyScore = Math.Min(100, rep.TrustSafetyScore + tssDelta);
        }

        var (level, title, _, _, _) = CalculateTier(rep.PermanentReputationPoints);
        rep.CurrentTierLevel = level;
        rep.TierTitle = title;
        rep.UpdatedAt = DateTime.UtcNow;

        _db.ReputationTransactions.Add(new ReputationTransaction
        {
            UserId = userId,
            ActionType = actionType,
            PointsChanged = pointsDelta,
            TssChanged = tssDelta,
            SourceId = sourceId,
            Reason = reason ?? $"Thưởng điểm hành động: {actionType}"
        });

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Added {Points} PRP and {TSS} TSS to user {UserId} for {Action}", pointsDelta, tssDelta, userId, actionType);
    }

    public async Task DeductPointsAsync(
        Guid userId,
        string actionType,
        int pointsPenalty,
        int tssPenalty = 0,
        Guid? sourceId = null,
        string? reason = null,
        CancellationToken ct = default)
    {
        var rep = await GetOrCreateReputationAsync(userId, ct);

        rep.PermanentReputationPoints = Math.Max(0, rep.PermanentReputationPoints - pointsPenalty);
        rep.WeeklyContributionScore = Math.Max(0, rep.WeeklyContributionScore - pointsPenalty);
        rep.MonthlyContributionScore = Math.Max(0, rep.MonthlyContributionScore - pointsPenalty);

        if (tssPenalty > 0)
        {
            rep.TrustSafetyScore = Math.Max(0, rep.TrustSafetyScore - tssPenalty);
        }

        // Tự động phân cấp chế tài theo ngưỡng TSS
        if (rep.TrustSafetyScore < 20)
        {
            rep.IsSanctioned = true;
            rep.SanctionLevel = 4; // Banned
            rep.SanctionReason = reason ?? "Điểm tín nhiệm an toàn rơi xuống dưới 20 điểm.";
        }
        else if (rep.TrustSafetyScore < 40)
        {
            rep.IsSanctioned = true;
            rep.SanctionLevel = 3; // Mute 7 days
            rep.SanctionExpiresAt = DateTime.UtcNow.AddDays(7);
            rep.SanctionReason = reason ?? "Tạm đình chỉ quyền đăng bài 7 ngày do vi phạm nghiêm trọng.";
        }
        else if (rep.TrustSafetyScore < 60)
        {
            rep.IsSanctioned = true;
            rep.SanctionLevel = 2; // Sandbox Moderation
            rep.SanctionReason = reason ?? "Bài viết bắt buộc kiểm duyệt thủ công do điểm TSS dưới 60.";
        }
        else if (rep.TrustSafetyScore < 80)
        {
            rep.SanctionLevel = 1; // Warning
        }

        var (level, title, _, _, _) = CalculateTier(rep.PermanentReputationPoints);
        rep.CurrentTierLevel = level;
        rep.TierTitle = title;
        rep.UpdatedAt = DateTime.UtcNow;

        _db.ReputationTransactions.Add(new ReputationTransaction
        {
            UserId = userId,
            ActionType = actionType,
            PointsChanged = -pointsPenalty,
            TssChanged = -tssPenalty,
            SourceId = sourceId,
            Reason = reason ?? $"Trừ điểm vi phạm: {actionType}"
        });

        await _db.SaveChangesAsync(ct);
        _logger.LogWarning("Deducted {Points} PRP and {TSS} TSS from user {UserId}. New TSS: {NewTSS}", pointsPenalty, tssPenalty, userId, rep.TrustSafetyScore);
    }

    public async Task ApplySanctionAsync(
        Guid userId,
        int sanctionLevel,
        int? durationDays,
        string reason,
        string issuedBy,
        CancellationToken ct = default)
    {
        var rep = await GetOrCreateReputationAsync(userId, ct);

        rep.IsSanctioned = true;
        rep.SanctionLevel = sanctionLevel;
        rep.SanctionReason = reason;
        rep.SanctionExpiresAt = durationDays.HasValue ? DateTime.UtcNow.AddDays(durationDays.Value) : null;
        rep.UpdatedAt = DateTime.UtcNow;

        _db.UserSanctions.Add(new UserSanction
        {
            UserId = userId,
            SanctionLevel = sanctionLevel,
            Reason = reason,
            StartedAt = DateTime.UtcNow,
            ExpiresAt = rep.SanctionExpiresAt,
            IsActive = true,
            IssuedBy = issuedBy
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task PardonUserAsync(
        Guid userId,
        string reason,
        bool restorePoints = true,
        CancellationToken ct = default)
    {
        var rep = await GetOrCreateReputationAsync(userId, ct);

        rep.IsSanctioned = false;
        rep.SanctionLevel = 0;
        rep.SanctionReason = null;
        rep.SanctionExpiresAt = null;

        if (restorePoints)
        {
            rep.TrustSafetyScore = Math.Max(rep.TrustSafetyScore, 80);
        }

        // Deactivate all active sanctions
        var activeSanctions = await _db.UserSanctions
            .Where(s => s.UserId == userId && s.IsActive)
            .ToListAsync(ct);
        foreach (var s in activeSanctions)
        {
            s.IsActive = false;
        }

        _db.ReputationTransactions.Add(new ReputationTransaction
        {
            UserId = userId,
            ActionType = "APPEAL_REVERSAL",
            PointsChanged = 0,
            TssChanged = restorePoints ? 20 : 0,
            Reason = $"Ân xá / Gỡ phạt: {reason}"
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<ReputationTransactionDto>> GetHistoryAsync(Guid userId, int limit = 50, CancellationToken ct = default)
    {
        var txs = await _db.ReputationTransactions
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

        return txs.Select(t => new ReputationTransactionDto
        {
            Id = t.Id,
            ActionType = t.ActionType,
            ActionDescription = GetActionDescription(t.ActionType),
            PointsChanged = t.PointsChanged,
            TssChanged = t.TssChanged,
            SourceId = t.SourceId,
            Reason = t.Reason,
            CreatedAt = t.CreatedAt
        }).ToList();
    }

    private static string GetActionDescription(string actionType)
    {
        return actionType switch
        {
            "POST_CREATED" => "Đăng bài viết kỹ thuật mới",
            "LIKE_RECEIVED" => "Nhận lượt thích bài viết",
            "BOOKMARK_RECEIVED" => "Bài viết được lưu trữ tham khảo",
            "COMMENT_CREATED" => "Bình luận giải pháp kỹ thuật",
            "COMMENT_LIKE" => "Bình luận nhận được lượt thích",
            "BEST_SOLUTION" => "Được chọn làm giải pháp tốt nhất",
            "AI_QUALITY_BONUS" => "Thưởng bài viết chất lượng cao từ AI",
            "TOXIC_PENALTY" => "Phạt nội dung ngôn từ độc hại / vi phạm",
            "REPORT_PENALTY" => "Bị cộng đồng báo cáo và Admin duyệt ẩn",
            "INACTIVITY_DECAY" => "Suy giảm điểm đóng góp do không hoạt động",
            "PASSIVE_RECOVERY" => "Phục hồi điểm tín nhiệm theo tuần hạnh kiểm tốt",
            "ACTIVE_RECOVERY" => "Chuộc lỗi bằng đóng góp kỹ thuật chất lượng",
            "APPEAL_REVERSAL" => "Khôi phục điểm do khiếu nại thành công",
            _ => actionType
        };
    }

    public async Task ProcessInactivityDecayAsync(CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-14);
        var inactiveUsers = await _db.UserReputations
            .Where(r => r.LastActivityAt < cutoff && (r.WeeklyContributionScore > 0 || r.MonthlyContributionScore > 0))
            .ToListAsync(ct);

        foreach (var u in inactiveUsers)
        {
            var oldWeekly = u.WeeklyContributionScore;
            var oldMonthly = u.MonthlyContributionScore;

            // Decay 15%
            u.WeeklyContributionScore = (int)Math.Round(u.WeeklyContributionScore * 0.85);
            u.MonthlyContributionScore = (int)Math.Round(u.MonthlyContributionScore * 0.85);
            u.ConsecutiveInactiveWeeks++;
            u.UpdatedAt = DateTime.UtcNow;

            var decayed = (oldWeekly - u.WeeklyContributionScore) + (oldMonthly - u.MonthlyContributionScore);
            if (decayed > 0)
            {
                _db.ReputationTransactions.Add(new ReputationTransaction
                {
                    UserId = u.UserId,
                    ActionType = "INACTIVITY_DECAY",
                    PointsChanged = -decayed,
                    TssChanged = 0,
                    Reason = $"Trừ suy giảm {decayed} điểm do không hoạt động trong 14 ngày qua."
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Processed inactivity decay for {Count} users", inactiveUsers.Count);
    }

    public async Task ProcessTssPassiveRecoveryAsync(CancellationToken ct = default)
    {
        var oneWeekAgo = DateTime.UtcNow.AddDays(-7);
        var eligibleUsers = await _db.UserReputations
            .Where(r => r.TrustSafetyScore < 80 && (!r.LastTssPassiveRecoveryAt.HasValue || r.LastTssPassiveRecoveryAt < oneWeekAgo))
            .ToListAsync(ct);

        foreach (var u in eligibleUsers)
        {
            // Kiểm tra xem 7 ngày qua có bị phạt không
            var recentPenalties = await _db.ReputationTransactions
                .AnyAsync(t => t.UserId == u.UserId && t.TssChanged < 0 && t.CreatedAt >= oneWeekAgo, ct);

            if (!recentPenalties)
            {
                var addTss = Math.Min(5, 80 - u.TrustSafetyScore);
                u.TrustSafetyScore += addTss;
                u.LastTssPassiveRecoveryAt = DateTime.UtcNow;
                u.ConsecutiveCleanWeeks++;
                u.UpdatedAt = DateTime.UtcNow;

                _db.ReputationTransactions.Add(new ReputationTransaction
                {
                    UserId = u.UserId,
                    ActionType = "PASSIVE_RECOVERY",
                    PointsChanged = 0,
                    TssChanged = addTss,
                    Reason = $"Phục hồi hạnh kiểm: +{addTss} TSS sau 7 ngày liên tục không vi phạm."
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Processed TSS passive recovery for {Count} users", eligibleUsers.Count);
    }

    public async Task<List<LeaderboardUserDto>> GetLeaderboardAsync(string period = "all", int top = 10, CancellationToken ct = default)
    {
        top = top <= 0 || top > 100 ? 10 : top;

        IQueryable<UserReputation> query = _db.UserReputations.AsNoTracking();

        List<UserReputation> sortedUsers;
        if (period.ToLower() == "weekly")
        {
            sortedUsers = await query
                .OrderByDescending(u => u.WeeklyContributionScore)
                .ThenByDescending(u => u.PermanentReputationPoints)
                .Take(top)
                .ToListAsync(ct);
        }
        else if (period.ToLower() == "monthly")
        {
            sortedUsers = await query
                .OrderByDescending(u => u.MonthlyContributionScore)
                .ThenByDescending(u => u.PermanentReputationPoints)
                .Take(top)
                .ToListAsync(ct);
        }
        else
        {
            sortedUsers = await query
                .OrderByDescending(u => u.PermanentReputationPoints)
                .Take(top)
                .ToListAsync(ct);
        }

        var result = new List<LeaderboardUserDto>();
        for (int i = 0; i < sortedUsers.Count; i++)
        {
            var u = sortedUsers[i];
            var (tierLevel, tierTitle, tierBadge, _, _) = CalculateTier(u.PermanentReputationPoints);

            var badges = new List<string> { tierBadge };
            if (i == 0) badges.Add("🏆 Top 1 Contributor");
            else if (i < 3) badges.Add("🥈 Top Contributor");

            var score = period.ToLower() switch
            {
                "weekly" => u.WeeklyContributionScore,
                "monthly" => u.MonthlyContributionScore,
                _ => u.PermanentReputationPoints
            };

            result.Add(new LeaderboardUserDto
            {
                UserId = u.UserId,
                Username = u.Username ?? $"Dev_{u.UserId.ToString()[..6]}",
                AvatarUrl = u.AvatarUrl,
                Rank = i + 1,
                Score = score,
                TierLevel = tierLevel,
                TierTitle = tierTitle,
                TierBadge = tierBadge,
                Badges = badges
            });
        }

        return result;
    }

    public async Task CloseLeaderboardPeriodAsync(string periodType, CancellationToken ct = default)
    {
        var isWeekly = periodType.ToUpper() == "WEEKLY";
        var periodCode = isWeekly
            ? $"{DateTime.UtcNow.Year}-W{(DateTime.UtcNow.DayOfYear / 7) + 1}"
            : $"{DateTime.UtcNow.Year}-M{DateTime.UtcNow.Month:D2}";

        var topUsers = await GetLeaderboardAsync(isWeekly ? "weekly" : "monthly", 10, ct);

        var snapshot = new LeaderboardSnapshot
        {
            PeriodType = isWeekly ? "WEEKLY" : "MONTHLY",
            PeriodCode = periodCode,
            ClosedAt = DateTime.UtcNow
        };

        foreach (var u in topUsers)
        {
            snapshot.Entries.Add(new LeaderboardEntry
            {
                UserId = u.UserId,
                Username = u.Username,
                AvatarUrl = u.AvatarUrl,
                Rank = u.Rank,
                Score = u.Score,
                TierTitle = u.TierTitle
            });

            // Vinh danh: Thưởng điểm PRP vĩnh viễn cho Top 1, 2, 3
            if (u.Rank == 1)
            {
                await AddPointsAsync(u.UserId, "WEEKLY_WINNER_BONUS", 50, 0, null, $"Vinh danh Top 1 Bảng xếp hạng {periodCode}", ct);
            }
            else if (u.Rank <= 3)
            {
                await AddPointsAsync(u.UserId, "TOP_CONTRIBUTOR_BONUS", 30, 0, null, $"Vinh danh Top {u.Rank} Bảng xếp hạng {periodCode}", ct);
            }
        }

        _db.LeaderboardSnapshots.Add(snapshot);

        // Reset điểm kỳ mới
        var allUsers = await _db.UserReputations.ToListAsync(ct);
        foreach (var u in allUsers)
        {
            if (isWeekly) u.WeeklyContributionScore = 0;
            else u.MonthlyContributionScore = 0;
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Closed leaderboard period {PeriodCode} with {Count} entries", periodCode, topUsers.Count);
    }
}
