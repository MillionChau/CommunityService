using Community.Application.Common.Models;
using Community.Application.DTOs;
using Community.Application.Features.Follows.Commands.ToggleFollowUser;
using Community.Application.Features.Follows.Queries.GetFollowers;
using Community.Application.Features.Follows.Queries.GetFollowing;
using Community.Application.Features.Follows.Queries.GetFollowSummary;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Community.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FollowsController : ApiControllerBase
{
    /// <summary>Theo dõi hoặc bỏ theo dõi tác giả (UC-30) — cần đăng nhập.</summary>
    [HttpPost("{targetUserId:guid}")]
    [Authorize]
    public async Task<ActionResult<ResponseModel<ToggleFollowResult>>> ToggleFollow(
        Guid targetUserId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new ToggleFollowUserCommand(targetUserId), cancellationToken);
        return Ok(result);
    }

    /// <summary>Lấy danh sách người đang theo dõi tác giả (Followers) (UC-30) — công khai.</summary>
    [HttpGet("{userId:guid}/followers")]
    [AllowAnonymous]
    public async Task<ActionResult<ResponseModel<PagedResponse<UserFollowDto>>>> GetFollowers(
        Guid userId, [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(new GetFollowersQuery(userId, pageIndex, pageSize), cancellationToken);
        return Ok(result);
    }

    /// <summary>Lấy danh sách tác giả mà người dùng đang theo dõi (Following) (UC-30) — công khai.</summary>
    [HttpGet("{userId:guid}/following")]
    [AllowAnonymous]
    public async Task<ActionResult<ResponseModel<PagedResponse<UserFollowDto>>>> GetFollowing(
        Guid userId, [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(new GetFollowingQuery(userId, pageIndex, pageSize), cancellationToken);
        return Ok(result);
    }

    /// <summary>Lấy tổng quan trạng thái theo dõi (số followers, following, đã follow chưa) (UC-30).</summary>
    [HttpGet("{userId:guid}/summary")]
    [AllowAnonymous]
    public async Task<ActionResult<ResponseModel<UserFollowSummaryDto>>> GetSummary(
        Guid userId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetFollowSummaryQuery(userId), cancellationToken);
        return Ok(result);
    }
}
