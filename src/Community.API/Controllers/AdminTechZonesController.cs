using Community.Application.Common.Models;
using Community.Application.DTOs;
using Community.Application.Features.TechZones.Commands.CreateTechZone;
using Community.Application.Features.TechZones.Commands.DeleteTechZone;
using Community.Application.Features.TechZones.Commands.ToggleTechZoneStatus;
using Community.Application.Features.TechZones.Commands.UpdateTechZone;
using Community.Application.Features.TechZones.Commands.UpdateTechZoneTags;
using Community.Application.Features.TechZones.Queries.GetAdminTechZones;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Community.API.Controllers;

[ApiController]
[Route("api/admin/techzones")]
[Authorize]
public class AdminTechZonesController : ApiControllerBase
{
    /// <summary>
    /// Bảng quản trị danh sách TechZone (UC-69) — phân trang, tìm kiếm theo tên/slug, lọc status.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ResponseModel<PagedResponse<TechZoneDto>>>> GetAll(
        [FromQuery] GetAdminTechZonesQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Tạo mới một TechZone (UC-70) — chỉ Admin.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ResponseModel<Guid>>> Create(
        CreateTechZoneCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Cập nhật thông tin chi tiết TechZone (UC-71) — chỉ Admin.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ResponseModel<bool>>> Update(
        Guid id, UpdateTechZoneCommand command, CancellationToken cancellationToken)
    {
        var updated = command with { Id = id };
        var result = await Mediator.Send(updated, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Bật / tắt trạng thái hoạt động của TechZone (UC-72) — chỉ Admin.
    /// </summary>
    [HttpPatch("{id:guid}/toggle-status")]
    public async Task<ActionResult<ResponseModel<int>>> ToggleStatus(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new ToggleTechZoneStatusCommand(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Xóa TechZone khi không còn bài viết ràng buộc (UC-73) — chỉ Admin.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ResponseModel<bool>>> Delete(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeleteTechZoneCommand(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Cập nhật danh sách tags/công nghệ thuộc TechZone (UC-74) — chỉ Admin.
    /// </summary>
    [HttpPut("{id:guid}/tags")]
    public async Task<ActionResult<ResponseModel<bool>>> UpdateTags(
        Guid id, [FromBody] string tags, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new UpdateTechZoneTagsCommand(id, tags), cancellationToken);
        return Ok(result);
    }
}
