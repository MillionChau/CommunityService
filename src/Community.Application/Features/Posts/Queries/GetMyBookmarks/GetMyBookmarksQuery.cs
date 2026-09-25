using AutoMapper;
using Community.Application.Common.Models;
using Community.Application.DTOs;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Community.Application.Features.Posts.Queries.GetMyBookmarks;

public record GetMyBookmarksQuery : IRequest<ResponseModel<PagedResponse<PostDto>>>
{
    /// <summary>Số trang (bắt đầu từ 1).</summary>
    public int Page { get; init; } = 1;
    /// <summary>Số bản ghi mỗi trang (mặc định 10, tối đa 50).</summary>
    public int PageSize { get; init; } = 10;
}

public class GetMyBookmarksQueryValidator : AbstractValidator<GetMyBookmarksQuery>
{
    public GetMyBookmarksQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0).WithMessage("Page must be greater than 0.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50).WithMessage("PageSize must be between 1 and 50.");
    }
}

/// <summary>
/// Danh sách bài viết đã lưu của người dùng hiện tại (FR-20 / UC-20),
/// sắp xếp theo thời điểm lưu mới nhất, có phân trang.
/// FR-21 (phân loại vào bộ sưu tập/thư mục) nằm ngoài phạm vi hiện tại.
/// </summary>
public class GetMyBookmarksQueryHandler : IRequestHandler<GetMyBookmarksQuery, ResponseModel<PagedResponse<PostDto>>>
{
    private readonly IPostBookmarkRepository _bookmarkRepository;
    private readonly IPostRepository _postRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IMapper _mapper;

    public GetMyBookmarksQueryHandler(
        IPostBookmarkRepository bookmarkRepository,
        IPostRepository postRepository,
        ICurrentUserService currentUser,
        IMapper mapper)
    {
        _bookmarkRepository = bookmarkRepository;
        _postRepository = postRepository;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<ResponseModel<PagedResponse<PostDto>>> Handle(GetMyBookmarksQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException("Authentication is required.");
        var userId = _currentUser.UserId.Value;

        var bookmarksQuery = _bookmarkRepository.GetByExpression(b => b.UserId == userId, cancellationToken);

        var totalCount = await bookmarksQuery.CountAsync(cancellationToken);

        var pageBookmarks = await bookmarksQuery
            .OrderByDescending(b => b.CreatedDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var postIds = pageBookmarks.Select(b => b.PostId).ToList();

        // Giữ thứ tự theo thời điểm lưu (mới nhất trước) thay vì thứ tự trả về từ DB
        var postsById = await _postRepository
            .GetByExpression(p => postIds.Contains(p.Id), cancellationToken)
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var dtos = pageBookmarks
            .Where(b => postsById.ContainsKey(b.PostId))
            .Select(b =>
            {
                var dto = _mapper.Map<PostDto>(postsById[b.PostId]);
                dto.IsBookmarkedByViewer = true; // đương nhiên: đây là danh sách đã lưu
                return dto;
            })
            .ToList();

        var paged = PagedResponse<PostDto>.Create(dtos, request.Page, request.PageSize, totalCount);
        return ResponseModel<PagedResponse<PostDto>>.Success(paged);
    }
}
