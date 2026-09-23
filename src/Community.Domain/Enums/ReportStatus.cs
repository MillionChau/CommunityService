namespace Community.Domain.Enums;

/// <summary>
/// Vòng đời một báo cáo vi phạm (PostReport) theo UC-23 / FR-23 của SRS:
/// Người dùng gửi report → Pending → Admin duyệt (Approved: ẩn bài vi phạm)
/// hoặc từ chối (Rejected: giữ nguyên bài viết).
/// </summary>
public enum ReportStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}
