using Community.Application.Common.Models;
using Community.Application.DTOs;
using Community.Application.Features.TechZones.Queries.GetActiveTechZones;
using Community.Application.Features.TechZones.Queries.GetTechZoneBySlug;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Community.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TechZonesController : ApiControllerBase
{
    /// <summary>
    /// Khám phá danh mục TechZone (UC-14): lấy danh sách các Zone đang kích hoạt — công khai cho khách.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ResponseModel<IEnumerable<TechZoneDto>>>> GetActive(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetActiveTechZonesQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Xem chi tiết TechZone theo slug (UC-14) — công khai cho khách.
    /// </summary>
    [HttpGet("{slug}")]
    [AllowAnonymous]
    public async Task<ActionResult<ResponseModel<TechZoneDto>>> GetBySlug(string slug, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetTechZoneBySlugQuery(slug), cancellationToken);
        return Ok(result);
    }
}
