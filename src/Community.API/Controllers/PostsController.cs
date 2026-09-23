using Community.Application.Common.Models;
using Community.Application.DTOs;
using Community.Application.Features.Posts.Commands.CreatePost;
using Community.Application.Features.Posts.Commands.DeletePost;
using Community.Application.Features.Posts.Commands.UpdatePost;
using Community.Application.Features.Posts.Queries.GetPostById;
using Community.Application.Features.Posts.Queries.GetPosts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Community.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PostsController : ApiControllerBase
{
    /// <summary>Xem bảng tin cộng đồng (FR-10): phân trang, tìm kiếm, lọc — công khai cho khách.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ResponseModel<PagedResponse<PostDto>>>> GetAll(
        [FromQuery] GetPostsQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>Xem chi tiết bài viết (FR-11) — công khai cho khách.</summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<ResponseModel<PostDto>>> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetPostByIdQuery(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>Đăng bài viết mới (FR-15) — cần đăng nhập; nội dung qua kiểm duyệt AI.</summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ResponseModel<Guid>>> Create(CreatePostCommand command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Data }, result);
    }

    /// <summary>Chỉnh sửa bài viết (FR-16) — chỉ tác giả.</summary>
    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<ResponseModel<bool>>> Update(Guid id, UpdatePostCommand command,
        CancellationToken cancellationToken)
    {
        var updated = command with { Id = id };
        var result = await Mediator.Send(updated, cancellationToken);
        return Ok(result);
    }

    /// <summary>Xóa bài viết (FR-17) — tác giả hoặc Admin (soft delete).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<ResponseModel<bool>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeletePostCommand { Id = id }, cancellationToken);
        return Ok(result);
    }
}
