using System.Text;
using System.Text.Json;
using Community.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Community.Infrastructure.Services;

/// <summary>
/// Dịch vụ kiểm duyệt nội dung thực tế — gọi HTTP POST sang QualityService (/api/v1/quality/analyze).
/// Cơ chế Degrade an toàn: Khi QualityService không phản hồi hoặc timeout (3s), hệ thống tự động
/// chấp nhận bài viết tạm thời (IsValid = true, safe) để đảm bảo trải nghiệm người dùng không bị gián đoạn.
/// </summary>
public class HttpContentModerationService : IContentModerationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpContentModerationService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public HttpContentModerationService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<HttpContentModerationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var baseUrl = configuration["QualityService:BaseUrl"] ?? "http://localhost:8002/";
        if (!baseUrl.EndsWith("/")) baseUrl += "/";
        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(4);
    }

    public async Task<ModerationResult> CheckAsync(string content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new ModerationResult
            {
                IsValid = false,
                Toxicity = "toxic",
                QualityScore = 0
            };
        }

        try
        {
            var payload = JsonSerializer.Serialize(new { content = content.Trim() }, JsonOptions);
            using var requestContent = new StringContent(payload, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync("api/v1/quality/analyze", requestContent, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;

                bool isValid = root.TryGetProperty("is_valid", out var validProp) && validProp.GetBoolean();

                string toxicity = "safe";
                if (root.TryGetProperty("toxicity", out var toxProp) && toxProp.TryGetProperty("label", out var labelProp))
                {
                    toxicity = labelProp.GetString() ?? "safe";
                }

                int qualityScore = 75;
                if (root.TryGetProperty("quality_score", out var scoreProp))
                {
                    qualityScore = scoreProp.GetInt32();
                }

                return new ModerationResult
                {
                    IsValid = isValid,
                    Toxicity = toxicity,
                    QualityScore = qualityScore,
                    RawResponse = responseBody
                };
            }

            _logger.LogWarning("QualityService returned status {StatusCode}. Gracefully degrading to default safe moderation.", response.StatusCode);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not reach QualityService (timeout/unavailable). Gracefully degrading to default safe moderation.");
        }

        // Safe Degrade Fallback
        return new ModerationResult
        {
            IsValid = true,
            Toxicity = "safe",
            QualityScore = 80
        };
    }
}
