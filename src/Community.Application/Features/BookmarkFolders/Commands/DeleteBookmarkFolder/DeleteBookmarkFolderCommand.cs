using Community.Application.Common.Exceptions;
using Community.Application.Common.Models;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Community.Application.Features.BookmarkFolders.Commands.DeleteBookmarkFolder;

public record DeleteBookmarkFolderCommand(Guid Id) : IRequest<ResponseModel<bool>>;

public class DeleteBookmarkFolderCommandHandler : IRequestHandler<DeleteBookmarkFolderCommand, ResponseModel<bool>>
{
    private readonly IBookmarkFolderRepository _folderRepository;
    private readonly IPostBookmarkRepository _bookmarkRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public DeleteBookmarkFolderCommandHandler(
        IBookmarkFolderRepository folderRepository,
        IPostBookmarkRepository bookmarkRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser)
    {
        _folderRepository = folderRepository;
        _bookmarkRepository = bookmarkRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ResponseModel<bool>> Handle(DeleteBookmarkFolderCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException("Authentication is required.");

        var userId = _currentUser.UserId.Value;

        var folder = await _folderRepository.GetByIdAsync(request.Id, cancellationToken);
        if (folder == null)
            throw new NotFoundException(nameof(BookmarkFolder), request.Id);

        if (folder.UserId != userId && !_currentUser.IsAdmin)
            throw new ForbiddenException("You can only delete your own bookmark folders.");

        // Gỡ liên kết folderId của các bookmark trong folder này về null
        var bookmarksInFolder = await _bookmarkRepository
            .GetByExpression(b => b.FolderId == request.Id, cancellationToken)
            .ToListAsync(cancellationToken);

        foreach (var bookmark in bookmarksInFolder)
        {
            bookmark.FolderId = null;
            await _bookmarkRepository.UpdateAsync(bookmark, cancellationToken);
        }

        await _folderRepository.DeleteAsync(folder.Id, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        return ResponseModel<bool>.Success(true, "Bookmark folder deleted successfully.");
    }
}
