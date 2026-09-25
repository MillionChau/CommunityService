using Community.API.Hubs;
using Community.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Community.API.RealTime;

/// <summary>
/// Cầu nối từ Application handlers sang SignalR: đẩy sự kiện tới các group
/// của CommunityHub. Đăng ký singleton vì IHubContext là singleton —
/// Application chỉ biết IRealTimeNotifier, không phụ thuộc SignalR.
/// </summary>
public class SignalRNotifier : IRealTimeNotifier
{
    private readonly IHubContext<CommunityHub> _hubContext;

    public SignalRNotifier(IHubContext<CommunityHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyFeedAsync(string eventName, object payload,
        CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group("feed").SendAsync(eventName, payload, cancellationToken);
    }

    public async Task NotifyPostAsync(Guid postId, string eventName, object payload,
        CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group($"post-{postId}").SendAsync(eventName, payload, cancellationToken);
    }
}
