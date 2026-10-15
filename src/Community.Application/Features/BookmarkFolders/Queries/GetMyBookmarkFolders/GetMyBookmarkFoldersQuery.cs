using AutoMapper;
using Community.Application.Common.Models;
using Community.Application.DTOs;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Community.Application.Features.BookmarkFolders.Queries.GetMyBookmarkFolders;

public record GetMyBookmarkFoldersQuery : IRequest<ResponseModel<IEnumerable<BookmarkFolderDto>>>;

public class GetMyBookmarkFoldersQueryHandler : IRequestHandler<GetMyBookmarkFoldersQuery, ResponseModel<IEnumerable<BookmarkFolderDto>>>
{
    private readonly IBookmarkFolderRepository _folderRepository;
    private readonly IPostBookmarkRepository _bookmarkRepository;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUser;

    public GetMyBookmarkFoldersQueryHandler(
        IBookmarkFolderRepository folderRepository,
        IPostBookmarkRepository bookmarkRepository,
        IMapper mapper,
        ICurrentUserService currentUser)
    {
        _folderRepository = folderRepository;
        _bookmarkRepository = bookmarkRepository;
        _mapper = mapper;
        _currentUser = currentUser;
    }

    public async Task<ResponseModel<IEnumerable<BookmarkFolderDto>>> Handle(GetMyBookmarkFoldersQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException("Authentication is required.");

        var userId = _currentUser.UserId.Value;

        var folders = (await _folderRepository.GetUserFoldersAsync(userId, cancellationToken)).ToList();

        // Cập nhật số lượng items thực tế trong từng folder
        var folderIds = folders.Select(f => f.Id).ToList();
        var counts = await _bookmarkRepository
            .GetByExpression(b => b.UserId == userId && b.FolderId != null && folderIds.Contains(b.FolderId.Value), cancellationToken)
            .GroupBy(b => b.FolderId!.Value)
            .Select(g => new { FolderId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.FolderId, x => x.Count, cancellationToken);

        var dtos = _mapper.Map<IEnumerable<BookmarkFolderDto>>(folders).ToList();
        foreach (var dto in dtos)
        {
            if (counts.TryGetValue(dto.Id, out var count))
            {
                dto.ItemsCount = count;
            }
        }

        return ResponseModel<IEnumerable<BookmarkFolderDto>>.Success(dtos);
    }
}
