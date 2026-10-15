namespace Community.Application.Interfaces;

/// <summary>
/// Một thông báo outbound gửi sang NotificationService qua internal API.
/// Type là mã số khớp enum NotificationType phía NotificationService:
/// 1 PostLiked, 2 PostCommented, 3 CommentReplied, 4 PostShared,
/// 5 ReportReviewed, 6 PostHidden, 99 System, 100 PendingReport.
/// </summary>
public record NotificationOutbound
{
    public Guid RecipientId { get; init; }
    public int Type { get; init; }
    public string Content { get; init; } = string.Empty;
    public string? LinkUrl { get; init; }
    public string SourceService { get; init; } = "community";
    public Guid? RelatedEntityId { get; init; }
    public Guid? RelatedPostId { get; init; }
    public Guid? ActorId { get; init; }
    public string? ActorName { get; init; }
}

/// <summary>
/// Mã loại thông báo — mirror của Notification.Domain.Enums.NotificationType
/// (không tham chiếu trực tiếp để giữ biên service).
/// </summary>
public static class NotificationTypes
{
    public const int PostLiked = 1;
    public const int PostCommented = 2;
    public const int CommentReplied = 3;
    public const int PostShared = 4;
    public const int ReportReviewed = 5;
    public const int PostHidden = 6;
    public const int System = 99;
    public const int PendingReport = 100;

    /// <summary>RecipientId đặc biệt: thông báo cho toàn bộ Admin (hàng chờ duyệt).</summary>
    public static readonly Guid AdminQueueRecipient = Guid.Empty;
}

/// <summary>
/// Client gọi NotificationService (HTTP internal + X-Api-Key).
/// Bắt buộc phải "best effort": lỗi notification KHÔNG được làm hỏng luồng chính
/// (like/comment vẫn phải thành công dù NotificationService chết).
/// </summary>
public interface INotificationClient
{
    Task SendAsync(NotificationOutbound notification, CancellationToken cancellationToken = default);

    Task SendManyAsync(IReadOnlyList<NotificationOutbound> notifications, CancellationToken cancellationToken = default);

    /// <summary>Admin đã xử lý báo cáo → gỡ khỏi hàng chờ PendingReport của NotificationService.</summary>
    Task ResolvePendingReportAsync(Guid reportId, CancellationToken cancellationToken = default);
}
