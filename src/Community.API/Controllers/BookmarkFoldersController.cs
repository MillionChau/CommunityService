using Community.Application.Common.Models;
using Community.Application.DTOs;
using Community.Application.Features.BookmarkFolders.Commands.CreateBookmarkFolder;
using Community.Application.Features.BookmarkFolders.Commands.DeleteBookmarkFolder;
using Community.Application.Features.BookmarkFolders.Commands.UpdateBookmarkFolder;
using Community.Application.Features.BookmarkFolders.Queries.GetMyBookmarkFolders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Community.API.Controllers;

[ApiController]
[Route("api/bookmarks/folders")]
[Authorize]
public class BookmarkFoldersController : ApiControllerBase
{
    /// <summary>
    /// Danh sách thư mục lưu trữ bookmark của người dùng hiện tại (UC-21).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ResponseModel<IEnumerable<BookmarkFolderDto>>>> GetMyFolders(
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetMyBookmarkFoldersQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Tạo mới thư mục lưu trữ bookmark (UC-21).
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ResponseModel<Guid>>> Create(
        CreateBookmarkFolderCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetMyFolders), null, result);
    }

    /// <summary>
    /// Cập nhật tên, mô tả, màu sắc thư mục lưu trữ bookmark (UC-21) — chỉ chủ sở hữu.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ResponseModel<bool>>> Update(
        Guid id, UpdateBookmarkFolderCommand command, CancellationToken cancellationToken)
    {
        var updated = command with { Id = id };
        var result = await Mediator.Send(updated, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Xóa thư mục lưu trữ bookmark (UC-21) — các bookmark trong thư mục được giữ lại và chuyển về không phân loại.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ResponseModel<bool>>> Delete(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeleteBookmarkFolderCommand(id), cancellationToken);
        return Ok(result);
    }
}
