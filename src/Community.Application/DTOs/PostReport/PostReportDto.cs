namespace Community.Application.DTOs;

public record PostReportDto
{
    public Guid Id { get; set; }
    public Guid PostId { get; set; }
    public Guid? ReporterId { get; set; }
    public string? Reason { get; set; }
    public int? Status { get; set; }
    public string? ReviewNote { get; set; }

    // Audit fields from AuditableEntity
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public Guid? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
}