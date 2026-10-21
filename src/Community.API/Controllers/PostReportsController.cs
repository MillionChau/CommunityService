using Community.Application.Common.Models;
using Community.Application.DTOs;
using Community.Application.Features.PostReport.Commands.CreatePostReport;
using Community.Application.Features.PostReport.Commands.ReviewPostReport;
using Community.Application.Features.PostReport.Queries.GetPendingReports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Community.API.Controllers;

/// <summary>
/// Báo cáo bài viết vi phạm (FR-23 / UC-23):
/// Người dùng tạo báo cáo → Admin xem hàng chờ → Admin duyệt (ẩn bài) hoặc từ chối.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PostReportsController : ApiControllerBase
{
    /// <summary>Gửi báo cáo vi phạm bài viết (FR-23) — cần đăng nhập, mỗi user 1 báo cáo Pending/post.</summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ResponseModel<PostReportDto>>> Create(CreatePostReportCommand command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>Xem hàng chờ báo cáo Pending — chỉ Admin.</summary>
    [HttpGet("pending")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ResponseModel<PagedResponse<PostReportDto>>>> GetPending(
        [FromQuery] GetPendingReportsQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>Duyệt (Approve=true → ẩn bài vi phạm) hoặc từ chối báo cáo — chỉ Admin.</summary>
    [HttpPut("{id:guid}/review")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ResponseModel<bool>>> Review(Guid id, [FromBody] ReviewRequest body,
        CancellationToken cancellationToken)
    {
        var command = new ReviewPostReportCommand
        {
            ReportId = id,
            Approve = body.Approve,
            ReviewNote = body.ReviewNote
        };
        var result = await Mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>Xem hàng chờ báo cáo bình luận Pending (UC-29 / UC-62) — Quản trị viên hoặc tài khoản kiểm thử.</summary>
    [HttpGet("comments/pending")]
    [Authorize]
    public async Task<ActionResult<ResponseModel<PagedResponse<CommentReportDto>>>> GetPendingCommentReports(
        [FromQuery] Community.Application.Features.Comments.Queries.GetPendingCommentReports.GetPendingCommentReportsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>Duyệt (Approve=true → ẩn bình luận) hoặc từ chối báo cáo bình luận (UC-29 / UC-63) — Quản trị viên hoặc tài khoản kiểm thử.</summary>
    [HttpPut("comments/{id:guid}/review")]
    [Authorize]
    public async Task<ActionResult<ResponseModel<bool>>> ReviewCommentReport(Guid id, [FromBody] ReviewRequest body,
        CancellationToken cancellationToken)
    {
        var command = new Community.Application.Features.Comments.Commands.ReviewCommentReport.ReviewCommentReportCommand
        {
            ReportId = id,
            Approve = body.Approve,
            ReviewNote = body.ReviewNote
        };
        var result = await Mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    public record ReviewRequest(bool Approve, string? ReviewNote);
}
