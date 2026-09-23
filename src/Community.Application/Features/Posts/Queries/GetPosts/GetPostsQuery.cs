using AutoMapper;
using Community.Application.Common.Models;
using Community.Application.DTOs;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Community.Application.Features.Posts.Queries.GetPosts;

public record GetPostsQuery : IRequest<ResponseModel<PagedResponse<PostDto>>>
{
    /// <summary>Số trang (bắt đầu từ 1).</summary>
    public int Page { get; init; } = 1;
    /// <summary>Số bản ghi mỗi trang (mặc định 10, tối đa 50).</summary>
    public int PageSize { get; init; } = 10;
    /// <summary>Từ khóa tìm kiếm trong nội dung bài viết (FR-12).</summary>
    public string? Search { get; init; }
    /// <summary>Lọc theo trạng thái (FR-13). Null = mặc định.</summary>
    public int? Status { get; init; }
}

public class GetPostsQueryValidator : AbstractValidator<GetPostsQuery>
{
    public GetPostsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0).WithMessage("Page must be greater than 0.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50).WithMessage("PageSize must be between 1 and 50.");
    }
}

/// <summary>
/// Xem bảng tin cộng đồng có phân trang/tìm kiếm/lọc (FR-10 → FR-13 / UC-10 → UC-13).
/// Người dùng thường chỉ thấy bài Published; Admin xem được cả Hidden/Draft qua filter.
/// </summary>
public class GetPostsQueryHandler : IRequestHandler<GetPostsQuery, ResponseModel<PagedResponse<PostDto>>>
{
    private readonly IPostRepository _postRepository;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUser;

    public GetPostsQueryHandler(IPostRepository postRepository, IMapper mapper, ICurrentUserService currentUser)
    {
        _postRepository = postRepository;
        _mapper = mapper;
        _currentUser = currentUser;
    }

    public async Task<ResponseModel<PagedResponse<PostDto>>> Handle(GetPostsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _postRepository.GetByExpression(cancellationToken: cancellationToken);

        // Mặc định: chỉ hiển thị bài đã xuất bản, trừ khi Admin lọc explicitly
        if (request.Status.HasValue)
        {
            // Admin mới được lọc trạng thái đặc biệt
            if (request.Status != (int)ContentStatus.Published && !_currentUser.IsAdmin)
                request = request with { Status = (int)ContentStatus.Published };
            query = query.Where(p => p.Status == request.Status);
        }
        else
        {
            query = query.Where(p => p.Status == (int)ContentStatus.Published);
        }

        // Tìm kiếm theo từ khóa trong nội dung (FR-12)
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var keyword = request.Search.Trim();
            query = query.Where(p => p.Content != null && p.Content.ToLower().Contains(keyword.ToLower()));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var posts = await query
            .OrderByDescending(p => p.CreatedDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var dtos = _mapper.Map<IEnumerable<PostDto>>(posts).ToList();
        var paged = PagedResponse<PostDto>.Create(dtos, request.Page, request.PageSize, totalCount);

        return ResponseModel<PagedResponse<PostDto>>.Success(paged);
    }
}
