using Community.Application.Common.Models;
using Community.Application.DTOs;
using Community.Application.Features.Posts.Commands.DeletePost;
using Community.Application.Features.Posts.Commands.HidePost;
using Community.Application.Features.Posts.Commands.RestorePost;
using Community.Application.Features.Posts.Queries.GetAdminPosts;
using Community.Application.Features.Posts.Queries.GetPostById;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Community.API.Controllers;

[ApiController]
[Route("api/admin/posts")]
[Authorize]
public class AdminPostsController : ApiControllerBase
{
    /// <summary>
    /// Bảng quản trị danh sách toàn bộ bài viết (UC-52) — phân trang, lọc theo status, isApproved, techzone, từ khóa.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ResponseModel<PagedResponse<PostDto>>>> GetAll(
        [FromQuery] GetAdminPostsQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Xem chi tiết bài viết phía Admin (UC-53).
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ResponseModel<PostDto>>> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetPostByIdQuery(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Chủ động ẩn bài viết vi phạm (UC-54) — chuyển status sang Hidden (2).
    /// </summary>
    [HttpPut("{id:guid}/hide")]
    public async Task<ActionResult<ResponseModel<bool>>> Hide(
        Guid id, [FromBody] HidePostRequest? body, CancellationToken cancellationToken)
    {
        var command = new HidePostCommand
        {
            Id = id,
            Reason = body?.Reason
        };
        var result = await Mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Khôi phục bài viết đã bị ẩn (UC-55) — chuyển status về Published (1).
    /// </summary>
    [HttpPut("{id:guid}/restore")]
    public async Task<ActionResult<ResponseModel<bool>>> Restore(Guid id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new RestorePostCommand { Id = id }, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Xóa bài viết vi phạm (UC-56) — xóa bài viết khỏi hệ thống.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ResponseModel<bool>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeletePostCommand { Id = id }, cancellationToken);
        return Ok(result);
    }

    public record HidePostRequest(string? Reason);
}
