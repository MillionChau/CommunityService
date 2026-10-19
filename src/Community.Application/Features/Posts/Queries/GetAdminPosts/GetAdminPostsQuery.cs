using AutoMapper;
using Community.Application.Common.Exceptions;
using Community.Application.Common.Models;
using Community.Application.DTOs;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Community.Application.Features.Posts.Queries.GetAdminPosts;

public record GetAdminPostsQuery : IRequest<ResponseModel<PagedResponse<PostDto>>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? Search { get; init; }
    public int? Status { get; init; }
    public int? IsApproved { get; init; }
    public Guid? TechZoneId { get; init; }
}

public class GetAdminPostsQueryValidator : AbstractValidator<GetAdminPostsQuery>
{
    public GetAdminPostsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0).WithMessage("Page must be greater than 0.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
    }
}

/// <summary>
/// Quản trị viên xem danh sách toàn bộ bài viết (UC-52) kèm bộ lọc chi tiết:
/// Trạng thái (Draft=0, Published=1, Hidden=2, Deleted=3), trạng thái kiểm duyệt (IsApproved), TechZone, từ khóa.
/// </summary>
public class GetAdminPostsQueryHandler : IRequestHandler<GetAdminPostsQuery, ResponseModel<PagedResponse<PostDto>>>
{
    private readonly IPostRepository _postRepository;
    private readonly ITechZoneRepository _techZoneRepository;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUser;

    public GetAdminPostsQueryHandler(
        IPostRepository postRepository,
        ITechZoneRepository techZoneRepository,
        IMapper mapper,
        ICurrentUserService currentUser)
    {
        _postRepository = postRepository;
        _techZoneRepository = techZoneRepository;
        _mapper = mapper;
        _currentUser = currentUser;
    }

    public async Task<ResponseModel<PagedResponse<PostDto>>> Handle(GetAdminPostsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.IsAdmin)
            throw new ForbiddenException("Only administrators can access admin posts management.");

        var query = _postRepository.GetByExpression(cancellationToken: cancellationToken);

        // Lọc theo TechZone
        if (request.TechZoneId.HasValue)
        {
            query = query.Where(p => p.TechZoneId == request.TechZoneId.Value);
        }

        // Lọc theo Status nếu có
        if (request.Status.HasValue)
        {
            query = query.Where(p => p.Status == request.Status.Value);
        }
        else
        {
            // Mặc định Admin thấy tất cả trừ bài đã xóa vĩnh viễn
            query = query.Where(p => p.Status != (int)ContentStatus.Deleted);
        }

        // Lọc theo IsApproved (kiểm duyệt)
        if (request.IsApproved.HasValue)
        {
            query = query.Where(p => p.IsApproved == request.IsApproved.Value);
        }

        // Tìm kiếm từ khóa trong Content hoặc Tags
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var rawSearch = request.Search.Trim().ToLower();
            var searchTokens = rawSearch.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var token in searchTokens)
            {
                var term = token;
                query = query.Where(p =>
                    (p.Content != null && p.Content.ToLower().Contains(term)) ||
                    (p.Tags != null && p.Tags.ToLower().Contains(term)));
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var posts = await query
            .OrderByDescending(p => p.CreatedDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var dtos = _mapper.Map<IEnumerable<PostDto>>(posts).ToList();

        // Map thông tin TechZone
        var zoneIds = posts.Where(p => p.TechZoneId.HasValue).Select(p => p.TechZoneId!.Value).Distinct().ToList();
        if (zoneIds.Count > 0)
        {
            var zones = await _techZoneRepository.GetByExpression(z => zoneIds.Contains(z.Id), cancellationToken)
                .AsNoTracking()
                .ToDictionaryAsync(z => z.Id, cancellationToken);

            foreach (var dto in dtos)
            {
                if (dto.TechZoneId.HasValue && zones.TryGetValue(dto.TechZoneId.Value, out var zone))
                {
                    dto.TechZoneName = zone.Name;
                    dto.TechZoneSlug = zone.Slug;
                }
            }
        }

        var pagedResponse = PagedResponse<PostDto>.Create(dtos, request.Page, request.PageSize, totalCount);
        return ResponseModel<PagedResponse<PostDto>>.Success(pagedResponse);
    }
}

