using Community.Application.Interfaces;

namespace Community.Infrastructure.Services;

/// <summary>
/// Stub kiểm duyệt nội dung — PHIÊN NÀY CHƯA GỌI QualityService THẬT.
/// Hợp đồng cuối cùng sẽ POST sang QualityService /api/v1/quality/analyze
/// (request: { content, content_id, user_id } → response: { is_valid, toxicity.label, quality_score }).
/// TODO (phiên sau): HttpClient + Polly retry + degrade an toàn khi QualityService chết:
/// trả IsValid=true kèm cờ pendingReview để hàng chờ Admin xử lý tay.
/// </summary>
public class StubContentModerationService : IContentModerationService
{
    public Task<ModerationResult> CheckAsync(string content, CancellationToken cancellationToken = default)
    {
        // Luôn chấp nhận nội dung — mọi bài viết sẽ Published.
        var result = new ModerationResult
        {
            IsValid = true,
            Toxicity = "safe",
            QualityScore = 80
        };

        return Task.FromResult(result);
    }
}
