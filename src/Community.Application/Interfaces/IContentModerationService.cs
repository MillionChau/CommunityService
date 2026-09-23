namespace Community.Application.Interfaces;

/// <summary>
/// Kết quả kiểm duyệt nội dung. Trường khớp với response của
/// QualityService POST /api/v1/quality/analyze (is_valid, toxicity.label, quality_score).
/// </summary>
public class ModerationResult
{
    /// <summary>True khi nội dung được phép hiển thị (is_it=True và toxicity=safe).</summary>
    public bool IsValid { get; set; }

    /// <summary>safe | toxic | suspicious (từ QualityService).</summary>
    public string Toxicity { get; set; } = "safe";

    /// <summary>Điểm chất lượng 0-100 do AI chấm.</summary>
    public int QualityScore { get; set; }

    /// <summary>Chi tiết bổ sung (issues, cleaned_text...) — dạng raw JSON để linh hoạt.</summary>
    public string? RawResponse { get; set; }
}

/// <summary>
/// Kiểm duyệt nội dung bài viết/bình luận bằng AI (SRS UC-15, UC-24, tính năng Kiểm duyệt nội dung tự động).
/// Phiên này mới chỉ cài đặt stub; tích hợp thật sang QualityService sẽ làm ở phiên sau.
/// </summary>
public interface IContentModerationService
{
    /// <summary>
    /// Phân tích nội dung và trả về kết quả kiểm duyệt.
    /// Implementation PHẢI degrade an toàn: khi service AI không khả dụng, trả về
    /// kết quả trung lập (IsValid = true) kèm cờ pendingReview để hàng chờ Admin xử lý.
    /// </summary>
    Task<ModerationResult> CheckAsync(string content, CancellationToken cancellationToken = default);
}
