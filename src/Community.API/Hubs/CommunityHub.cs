using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Community.API.Hubs;

/// <summary>
/// SignalR hub trung tâm cho realtime của bài viết/bình luận (FR-19 → FR-28).
/// Cấu trúc group:
///   - "feed"          : mọi client đang xem bảng tin — tự động tham gia khi kết nối.
///   - "post-{postId}" : client đang mở trang chi tiết một bài viết (gọi JoinPost/LeavePost).
/// Xác thực: JWT truyền qua query string ?access_token=... (xử lý trong DependencyInjection
/// tầng Infrastructure). Kết nối ẩn danh vẫn được chấp nhận — chỉ xem được sự kiện public.
/// </summary>
[AllowAnonymous]
public class CommunityHub : Hub
{
    private const string FeedGroup = "feed";

    /// <summary>Mọi client khi kết nối đều vào group "feed" để nhận sự kiện bảng tin.</summary>
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, FeedGroup);
        await base.OnConnectedAsync();
    }

    /// <summary>Rời group "feed" khi ngắt kết nối.</summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, FeedGroup);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Client gọi khi mở trang chi tiết bài viết — tham gia group post-{postId}
    /// để nhận comment-created/like-changed... realtime của bài đó.
    /// </summary>
    public Task JoinPost(Guid postId)
        => Groups.AddToGroupAsync(Context.ConnectionId, $"post-{postId}");

    /// <summary>Client gọi khi rời trang chi tiết bài viết.</summary>
    public Task LeavePost(Guid postId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"post-{postId}");
}
