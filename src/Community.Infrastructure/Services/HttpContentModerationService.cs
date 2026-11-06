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

        var baseUrl = configuration["QualityService:BaseUrl"] ?? "http://localhost:8001/";
        if (!baseUrl.EndsWith("/")) baseUrl += "/";
        _httpClient.BaseAddress = new Uri(baseUrl);
        var timeoutSeconds = configuration.GetValue<int>("QualityService:TimeoutSeconds", 15);
        _httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
    }

    public Task<ModerationResult> CheckAsync(string content, CancellationToken cancellationToken = default)
        => CheckAsync(content, "post", cancellationToken);

    public async Task<ModerationResult> CheckAsync(string content, string contentType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new ModerationResult
            {
                IsValid = false,
                Toxicity = "toxic",
                QualityScore = 0,
                Reason = "Nội dung không được để trống."
            };
        }

        try
        {
            var payload = JsonSerializer.Serialize(new { content = content.Trim(), content_type = contentType }, JsonOptions);
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

                string? reason = null;
                if (!isValid)
                {
                    if (toxicity == "toxic" || toxicity == "suspicious")
                    {
                        reason = "Nội dung chứa từ ngữ độc hại hoặc không phù hợp";
                    }
                    else if (root.TryGetProperty("is_it", out var isItProp) && !isItProp.GetBoolean())
                    {
                        reason = "Nội dung không thuộc lĩnh vực công nghệ thông tin (IT)";
                    }
                    else
                    {
                        reason = "Nội dung vi phạm tiêu chuẩn cộng đồng";
                    }
                }

                _logger.LogInformation("QualityService moderation completed. ContentType: {ContentType}, IsValid: {IsValid}, Toxicity: {Toxicity}, QualityScore: {QualityScore}, Reason: {Reason}",
                    contentType, isValid, toxicity, qualityScore, reason);

                return new ModerationResult
                {
                    IsValid = isValid,
                    Toxicity = toxicity,
                    QualityScore = qualityScore,
                    Reason = reason,
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
