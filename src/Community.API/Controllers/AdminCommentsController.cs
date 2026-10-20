using Community.Application.Common.Models;
using Community.Application.DTOs;
using Community.Application.Features.Comments.Commands.DeleteComment;
using Community.Application.Features.Comments.Commands.HideComment;
using Community.Application.Features.Comments.Commands.RestoreComment;
using Community.Application.Features.Comments.Queries.GetAdminCommentById;
using Community.Application.Features.Comments.Queries.GetAdminComments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Community.API.Controllers;

[ApiController]
[Route("api/admin/comments")]
[Authorize]
public class AdminCommentsController : ApiControllerBase
{
    /// <summary>
    /// Bảng quản trị danh sách toàn bộ bình luận (UC-57) — phân trang, lọc theo status, isApproved, postId, từ khóa.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ResponseModel<PagedResponse<CommentDto>>>> GetAll(
        [FromQuery] GetAdminCommentsQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Xem chi tiết một bình luận phía Admin (UC-58).
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ResponseModel<CommentDto>>> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetAdminCommentByIdQuery(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Chủ động ẩn bình luận vi phạm quy chuẩn cộng đồng (UC-59) — chuyển status sang Hidden (2).
    /// </summary>
    [HttpPut("{id:guid}/hide")]
    public async Task<ActionResult<ResponseModel<bool>>> Hide(
        Guid id, [FromBody] HideCommentRequest? body, CancellationToken cancellationToken)
    {
        var command = new HideCommentCommand
        {
            Id = id,
            Reason = body?.Reason
        };
        var result = await Mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Khôi phục bình luận đã bị ẩn (UC-60) — chuyển status về Published (1).
    /// </summary>
    [HttpPut("{id:guid}/restore")]
    public async Task<ActionResult<ResponseModel<bool>>> Restore(Guid id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new RestoreCommentCommand { Id = id }, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Xóa bình luận vi phạm khỏi hệ thống (UC-61).
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ResponseModel<bool>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeleteCommentCommand { Id = id }, cancellationToken);
        return Ok(result);
    }

    public record HideCommentRequest(string? Reason);
}
