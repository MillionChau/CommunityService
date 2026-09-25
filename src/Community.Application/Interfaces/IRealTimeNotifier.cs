namespace Community.Application.Interfaces;

/// <summary>
/// Gửi sự kiện realtime đến các client qua SignalR (FR realtime cho bài viết/bình luận).
/// Implement ở tầng API bằng IHubContext — Application không phụ thuộc SignalR.
/// </summary>
public interface IRealTimeNotifier
{
    /// <summary>Broadcast tới group "feed" (mọi client đang xem bảng tin).</summary>
    Task NotifyFeedAsync(string eventName, object payload, CancellationToken cancellationToken = default);

    /// <summary>Broadcast tới group "post-{postId}" (client đang mở chi tiết bài viết).</summary>
    Task NotifyPostAsync(Guid postId, string eventName, object payload, CancellationToken cancellationToken = default);
}
