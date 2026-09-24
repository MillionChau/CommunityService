using Community.Application.Common.Models;
using Community.Application.DTOs;
using Community.Application.Features.Comments.Commands.DeleteComment;
using Community.Application.Features.Comments.Queries.GetCommentsByPost;
using Community.Application.Features.Posts.Commands.CreateComment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Community.API.Controllers;
[ApiController]
[Route("api/[controller]")]
public class CommentController : ApiControllerBase
{
    /// <summary>Xem bình luận của một bài viết (FR-24 ngữ cảnh đọc) — công khai.</summary>
    [HttpGet("post/{postId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<ResponseModel<IEnumerable<CommentDto>>>> GetByPost(Guid postId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetCommentsByPostQuery(postId), cancellationToken);
        return Ok(result);
    }

    /// <summary>Bình luận / trả lời bình luận (FR-24, FR-25) — cần đăng nhập.</summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ResponseModel<CommentDto>>> Create(CreateCommentCommand command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>Xóa bình luận (FR-27) — tác giả bình luận hoặc Admin.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<ResponseModel<bool>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeleteCommentCommand { Id = id }, cancellationToken);
        return Ok(result);
    }
}