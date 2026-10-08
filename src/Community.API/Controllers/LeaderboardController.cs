using Community.Application.Common.Models;
using Community.Application.DTOs.Leaderboard;
using Community.Application.Features.Leaderboard.Queries.GetLeaderboard;
using Microsoft.AspNetCore.Mvc;

namespace Community.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LeaderboardController : ApiControllerBase
{
    /// <summary>
    /// Lấy bảng xếp hạng nhà phát triển / thành viên cộng đồng (FR-45, FR-46 / UC-45, UC-46).
    /// </summary>
    /// <param name="top">Số lượng người dùng top đầu (mặc định 10, tối đa 100)</param>
    /// <param name="period">Khoảng thời gian: all, weekly, monthly</param>
    [HttpGet]
    [ProducesResponseType(typeof(ResponseModel<List<LeaderboardUserDto>>), 200)]
    public async Task<IActionResult> GetLeaderboard([FromQuery] int top = 10, [FromQuery] string period = "all")
    {
        var result = await Mediator.Send(new GetLeaderboardQuery { Top = top, Period = period });
        return Ok(result);
    }
}
