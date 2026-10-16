using AutoMapper;
using Community.Application.Common.Models;
using Community.Application.DTOs;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Community.Application.Features.Posts.Queries.GetDrafts;

/// <summary>
/// UC-18: Truy xuất danh sách bài viết bản nháp của người dùng hiện tại có phân trang.
/// </summary>
public record GetDraftPostsQuery : IRequest<ResponseModel<PagedResponse<PostDto>>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}

public class GetDraftPostsQueryValidator : AbstractValidator<GetDraftPostsQuery>
{
    public GetDraftPostsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0).WithMessage("Page must be greater than 0.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50).WithMessage("PageSize must be between 1 and 50.");
    }
}

public class GetDraftPostsQueryHandler : IRequestHandler<GetDraftPostsQuery, ResponseModel<PagedResponse<PostDto>>>
{
    private readonly IPostRepository _postRepository;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUser;

    public GetDraftPostsQueryHandler(
        IPostRepository postRepository,
        IMapper mapper,
        ICurrentUserService currentUser)
    {
        _postRepository = postRepository;
        _mapper = mapper;
        _currentUser = currentUser;
    }

    public async Task<ResponseModel<PagedResponse<PostDto>>> Handle(GetDraftPostsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException("Authentication is required.");

        var userId = _currentUser.UserId.Value;

        // Chỉ lấy bài viết của chính người dùng hiện tại, có trạng thái là Draft và chưa bị xóa mềm
        var query = _postRepository.GetByExpression(
            p => p.AuthorId == userId &&
                 p.Status == (int)ContentStatus.Draft &&
                 (p.IsDeleted == null || p.IsDeleted == 0),
            cancellationToken);

        var totalCount = await query.CountAsync(cancellationToken);

        // Sắp xếp bản nháp theo thời gian sửa đổi gần nhất (hoặc ngày tạo)
        var drafts = await query
            .OrderByDescending(p => p.ModifiedDate ?? p.CreatedDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var dtos = _mapper.Map<IEnumerable<PostDto>>(drafts).ToList();

        var paged = PagedResponse<PostDto>.Create(dtos, request.Page, request.PageSize, totalCount);
        return ResponseModel<PagedResponse<PostDto>>.Success(paged);
    }
}
