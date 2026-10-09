using System.Text;
using System.Text.Json;
using Community.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Community.Infrastructure.Services;

/// <summary>
/// Gọi NotificationService (internal API + header X-Api-Key).
/// Nguyên tắc best-effort: mọi lỗi (network, 5xx, timeout) đều được nuốt và log cảnh báo —
/// việc like/comment/share KHÔNG được phép thất bại chỉ vì NotificationService chết.
/// </summary>
public class NotificationHttpClient : INotificationClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NotificationHttpClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public NotificationHttpClient(HttpClient httpClient, IConfiguration configuration, ILogger<NotificationHttpClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var apiKey = configuration["NotificationService:ApiKey"];
        if (!string.IsNullOrEmpty(apiKey))
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
    }

    public async Task SendAsync(NotificationOutbound notification, CancellationToken cancellationToken = default)
        => await SendManyAsync(new[] { notification }, cancellationToken);

    public async Task SendManyAsync(IReadOnlyList<NotificationOutbound> notifications, CancellationToken cancellationToken = default)
    {
        if (notifications.Count == 0) return;

        try
        {
            var payload = JsonSerializer.Serialize(new { items = notifications }, JsonOptions);
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync("api/internal/notifications", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "NotificationService returned {StatusCode} when pushing {Count} notification(s).",
                    (int)response.StatusCode, notifications.Count);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // TaskCanceledException: timeout 3s — NotificationService có thể đang chết
            _logger.LogWarning(ex,
                "Could not reach NotificationService — skipped {Count} notification(s). Main flow is unaffected.",
                notifications.Count);
        }
    }

    public async Task ResolvePendingReportAsync(Guid reportId, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new { reportId }, JsonOptions);
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Put, "api/internal/notifications/resolve-pending")
            {
                Content = content
            };
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("ResolvePendingReport failed with {StatusCode} for report {ReportId}.",
                    (int)response.StatusCode, reportId);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Could not reach NotificationService to resolve report {ReportId}.", reportId);
        }
    }
}
