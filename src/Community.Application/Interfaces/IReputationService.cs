using Community.Application.DTOs.Leaderboard;
using Community.Application.DTOs.Reputation;

namespace Community.Application.Interfaces;

/// <summary>
/// Giao diện xử lý Điểm uy tín, Điểm tín nhiệm, Cây cấp bậc và Bảng xếp hạng (SPEC-REP-01).
/// </summary>
public interface IReputationService
{
    /// <summary>
    /// Lấy thông tin điểm uy tín, cấp bậc và đặc quyền của người dùng
    /// </summary>
    Task<UserReputationDto> GetUserReputationAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Cộng điểm đóng góp (bài viết, bình luận, like, bookmark, best solution)
    /// </summary>
    Task AddPointsAsync(
        Guid userId,
        string actionType,
        int pointsDelta,
        int tssDelta = 0,
        Guid? sourceId = null,
        string? reason = null,
        CancellationToken ct = default);

    /// <summary>
    /// Trừ điểm do vi phạm tiêu chuẩn cộng đồng (nội dung độc hại, spam, report)
    /// </summary>
    Task DeductPointsAsync(
        Guid userId,
        string actionType,
        int pointsPenalty,
        int tssPenalty = 0,
        Guid? sourceId = null,
        string? reason = null,
        CancellationToken ct = default);

    /// <summary>
    /// Áp dụng chế tài xử phạt (Warning, Sandbox, Mute, Ban)
    /// </summary>
    Task ApplySanctionAsync(
        Guid userId,
        int sanctionLevel,
        int? durationDays,
        string reason,
        string issuedBy,
        CancellationToken ct = default);

    /// <summary>
    /// Ân xá / Gỡ bỏ chế tài xử phạt và khôi phục điểm nếu khiếu nại thành công
    /// </summary>
    Task PardonUserAsync(
        Guid userId,
        string reason,
        bool restorePoints = true,
        CancellationToken ct = default);

    /// <summary>
    /// Lấy lịch sử biến động điểm uy tín và tín nhiệm (Audit Log)
    /// </summary>
    Task<List<ReputationTransactionDto>> GetHistoryAsync(Guid userId, int limit = 50, CancellationToken ct = default);

    /// <summary>
    /// Tiến trình định kỳ trừ điểm ngủ đông / ít đóng góp (Inactivity Score Decay Engine)
    /// </summary>
    Task ProcessInactivityDecayAsync(CancellationToken ct = default);

    /// <summary>
    /// Tiến trình định kỳ phục hồi điểm tín nhiệm thụ động theo thời gian (+5 TSS mỗi tuần hạnh kiểm tốt)
    /// </summary>
    Task ProcessTssPassiveRecoveryAsync(CancellationToken ct = default);

    /// <summary>
    /// Lấy danh sách bảng xếp hạng người dùng (Tuần, Tháng, Tất cả)
    /// </summary>
    Task<List<LeaderboardUserDto>> GetLeaderboardAsync(string period = "all", int top = 10, CancellationToken ct = default);

    /// <summary>
    /// Chốt kỳ bảng xếp hạng tuần/tháng, lưu snapshot và reset điểm PCS
    /// </summary>
    Task CloseLeaderboardPeriodAsync(string periodType, CancellationToken ct = default);
}
