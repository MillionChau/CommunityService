using AutoMapper;
using Community.Application.Common.Helpers;
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
    /// <summary>Cursor (thời điểm ISO 8601 của bài viết cuối cùng) cho Infinite Scroll.</summary>
    public string? Cursor { get; init; }
    /// <summary>Từ khóa tìm kiếm trong nội dung bài viết (FR-12).</summary>
    public string? Search { get; init; }
    /// <summary>Lọc theo trạng thái (FR-13). Null = mặc định.</summary>
    public int? Status { get; init; }
    /// <summary>Lọc theo TechZoneId (UC-13, UC-14). Null = tất cả.</summary>
    public Guid? TechZoneId { get; init; }
    /// <summary>Lọc theo Slug của TechZone (UC-13, UC-14). Null = tất cả.</summary>
    public string? TechZoneSlug { get; init; }
    /// <summary>Lọc theo Tag công nghệ (UC-13). Null = tất cả.</summary>
    public string? Tag { get; init; }
    /// <summary>Lọc theo tác giả bài viết (Profile page). Null = tất cả.</summary>
    public Guid? AuthorId { get; init; }
    /// <summary>Chế độ sắp xếp: relevant (Dev-Rank cá nhân hóa), trending (xu hướng thịnh hành), latest (mới nhất).</summary>
    public string? SortBy { get; init; }
    /// <summary>Ẩn bài viết của chính người xem khi duyệt Bảng tin Newsfeed. Mặc định true khi không lọc theo AuthorId cụ thể.</summary>
    public bool? ExcludeOwnPosts { get; init; }
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
/// Xem bảng tin cộng đồng có phân trang/tìm kiếm/lọc (FR-10 → FR-13 / UC-10 → UC-14).
/// Hỗ trợ lọc theo TechZone, Tag công nghệ, và thuật toán xếp hạng Dev-Rank (SortBy = relevant | trending | latest).
/// </summary>
public class GetPostsQueryHandler : IRequestHandler<GetPostsQuery, ResponseModel<PagedResponse<PostDto>>>
{
    private readonly IPostRepository _postRepository;
    private readonly ITechZoneRepository _techZoneRepository;
    private readonly IPostLikeRepository _likeRepository;
    private readonly IPostBookmarkRepository _bookmarkRepository;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUser;
    private readonly IUserReputationRepository? _userReputationRepository;
    private readonly IPersonalizedFeedService? _personalizedFeedService;

    public GetPostsQueryHandler(
        IPostRepository postRepository,
        ITechZoneRepository techZoneRepository,
        IPostLikeRepository likeRepository,
        IPostBookmarkRepository bookmarkRepository,
        IMapper mapper,
        ICurrentUserService currentUser,
        IUserReputationRepository? userReputationRepository = null,
        IPersonalizedFeedService? personalizedFeedService = null)
    {
        _postRepository = postRepository;
        _techZoneRepository = techZoneRepository;
        _likeRepository = likeRepository;
        _bookmarkRepository = bookmarkRepository;
        _mapper = mapper;
        _currentUser = currentUser;
        _userReputationRepository = userReputationRepository;
        _personalizedFeedService = personalizedFeedService;
    }

    public async Task<ResponseModel<PagedResponse<PostDto>>> Handle(GetPostsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _postRepository.GetByExpression(cancellationToken: cancellationToken);

        // Lọc theo TechZone (UC-13, UC-14)
        if (request.TechZoneId.HasValue)
        {
            query = query.Where(p => p.TechZoneId == request.TechZoneId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(request.TechZoneSlug))
        {
            var zone = await _techZoneRepository.GetBySlugAsync(request.TechZoneSlug, cancellationToken);
            if (zone != null)
            {
                query = query.Where(p => p.TechZoneId == zone.Id);
            }
            else
            {
                query = query.Where(p => false);
            }
        }

        // Lọc theo Tag công nghệ (UC-13)
        if (!string.IsNullOrWhiteSpace(request.Tag))
        {
            var tag = request.Tag.Trim().ToLower();
            query = query.Where(p => p.Tags != null && p.Tags.ToLower().Contains(tag));
        }

        // Lọc theo tác giả bài viết (Profile)
        if (request.AuthorId.HasValue)
        {
            query = query.Where(p => p.AuthorId == request.AuthorId.Value || p.CreatedBy == request.AuthorId.Value);
        }
        else
        {
            // Ẩn các bài viết của chính người xem khi duyệt Bảng tin Newsfeed (User intent: feed dùng để khám phá cộng đồng)
            var shouldExcludeOwn = request.ExcludeOwnPosts ?? true;
            if (shouldExcludeOwn && _currentUser.IsAuthenticated && _currentUser.UserId.HasValue)
            {
                var myUserId = _currentUser.UserId.Value;
                query = query.Where(p => p.AuthorId != myUserId && p.CreatedBy != myUserId);
            }
        }

        // Mặc định: chỉ hiển thị bài đã xuất bản và đã được duyệt, trừ khi Admin lọc explicitly
        if (request.Status.HasValue)
        {
            // Admin mới được lọc trạng thái đặc biệt
            if (request.Status != (int)ContentStatus.Published && !_currentUser.IsAdmin)
                request = request with { Status = (int)ContentStatus.Published };
            query = query.Where(p => p.Status == request.Status);
            if (!_currentUser.IsAdmin)
                query = query.Where(p => p.IsApproved == null || p.IsApproved == 1);
        }
        else
        {
            query = query.Where(p => p.Status == (int)ContentStatus.Published && (p.IsApproved == null || p.IsApproved == 1));
        }

        // Tìm kiếm từ khóa đa trường / mờ trên Content và Tags (UC-12, FR-12)
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

        bool hasMore = false;
        string? nextCursor = null;
        List<Community.Domain.Entities.Post> posts;

        bool isPersonalizedRanking = _personalizedFeedService != null &&
            (string.Equals(request.SortBy, "relevant", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(request.SortBy, "trending", StringComparison.OrdinalIgnoreCase));

        if (isPersonalizedRanking)
        {
            // Giai đoạn 1: Thu hồi tập ứng viên (Candidate Retrieval)
            var candidateBatch = await query
                .OrderByDescending(p => p.CreatedDate)
                .Take(200)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            // Điền điểm tín nhiệm tác giả phục vụ tính Quality Score
            var authorIds = candidateBatch.Where(p => p.AuthorId.HasValue).Select(p => p.AuthorId!.Value).Distinct().ToList();
            Dictionary<Guid, int>? repMap = null;
            if (_userReputationRepository != null && authorIds.Count > 0)
            {
                repMap = await _userReputationRepository
                    .GetByExpression(r => authorIds.Contains(r.UserId), cancellationToken)
                    .ToDictionaryAsync(r => r.UserId, r => r.PermanentReputationPoints, cancellationToken);
            }

            // Giai đoạn 2: Tái xếp hạng cá nhân hóa bằng thuật toán Dev-Rank
            var userContext = await _personalizedFeedService!.BuildUserContextAsync(_currentUser.UserId, cancellationToken);
            var ranked = _personalizedFeedService.RankPosts(candidateBatch, userContext, request.SortBy, repMap);

            totalCount = Math.Max(totalCount, ranked.Count);

            var skip = (request.Page - 1) * request.PageSize;

            if (!string.IsNullOrWhiteSpace(request.Cursor))
            {
                var cursorIndex = -1;
                if (Guid.TryParse(request.Cursor, out var cursorId))
                {
                    cursorIndex = ranked.FindIndex(p => p.Id == cursorId);
                }
                if (cursorIndex == -1 && DateTime.TryParse(request.Cursor, null, System.Globalization.DateTimeStyles.RoundtripKind, out var cursorDate))
                {
                    cursorIndex = ranked.FindIndex(p => p.CreatedDate <= cursorDate);
                }

                if (cursorIndex >= 0)
                {
                    skip = cursorIndex + 1;
                }
                else
                {
                    skip = ranked.Count;
                }
            }

            posts = ranked.Skip(skip).Take(request.PageSize).ToList();
            hasMore = ranked.Count > skip + request.PageSize;

            if (posts.Count > 0 && hasMore)
            {
                nextCursor = posts[^1].CreatedDate.ToString("o");
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.Cursor) && DateTime.TryParse(request.Cursor, null, System.Globalization.DateTimeStyles.RoundtripKind, out var cursorDate))
        {
            var rawBatch = await query
                .Where(p => p.CreatedDate < cursorDate)
                .OrderByDescending(p => p.CreatedDate)
                .Take(request.PageSize + 1)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            if (rawBatch.Count > request.PageSize)
            {
                hasMore = true;
                posts = rawBatch.Take(request.PageSize).ToList();
            }
            else
            {
                posts = rawBatch;
            }

            if (posts.Count > 0 && hasMore)
            {
                nextCursor = posts[^1].CreatedDate.ToString("o");
            }
        }
        else
        {
            var rawBatch = await query
                .OrderByDescending(p => p.CreatedDate)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize + 1)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            if (rawBatch.Count > request.PageSize)
            {
                hasMore = true;
                posts = rawBatch.Take(request.PageSize).ToList();
            }
            else
            {
                posts = rawBatch;
            }

            if (posts.Count > 0 && hasMore)
            {
                nextCursor = posts[^1].CreatedDate.ToString("o");
            }
        }

        var dtos = _mapper.Map<IEnumerable<PostDto>>(posts).ToList();

        // Điền thông tin TechZone cho bài viết
        var zoneIds = posts.Where(p => p.TechZoneId.HasValue).Select(p => p.TechZoneId!.Value).Distinct().ToList();
        if (zoneIds.Count > 0)
        {
            var zonesById = await _techZoneRepository
                .GetByExpression(z => zoneIds.Contains(z.Id), cancellationToken)
                .ToDictionaryAsync(z => z.Id, cancellationToken);

            foreach (var dto in dtos)
            {
                if (dto.TechZoneId.HasValue && zonesById.TryGetValue(dto.TechZoneId.Value, out var z))
                {
                    dto.TechZoneName = z.Name;
                    dto.TechZoneSlug = z.Slug;
                }
            }
        }

        // Điền thông tin Điểm đóng góp và Cấp bậc (Tier) của tác giả (SPEC-REP-01)
        if (_userReputationRepository != null)
        {
            var authorIds = posts.Where(p => p.AuthorId.HasValue).Select(p => p.AuthorId!.Value).Distinct().ToList();
            if (authorIds.Count > 0)
            {
                var repByUserId = await _userReputationRepository
                    .GetByExpression(r => authorIds.Contains(r.UserId), cancellationToken)
                    .ToDictionaryAsync(r => r.UserId, cancellationToken);

                foreach (var dto in dtos)
                {
                    if (dto.AuthorId.HasValue && repByUserId.TryGetValue(dto.AuthorId.Value, out var rep))
                    {
                        dto.AuthorReputationPoints = rep.PermanentReputationPoints;
                        dto.AuthorTierTitle = rep.TierTitle;
                        dto.AuthorTierBadge = ReputationTierHelper.GetTierBadge(rep.PermanentReputationPoints);
                        if (!string.IsNullOrEmpty(rep.Username)) dto.AuthorUsername = rep.Username;
                        if (!string.IsNullOrEmpty(rep.AvatarUrl)) dto.AuthorAvatarUrl = rep.AvatarUrl;
                    }
                    else
                    {
                        dto.AuthorReputationPoints = 0;
                        dto.AuthorTierTitle = "Code Initiate";
                        dto.AuthorTierBadge = "🌱 Tân thủ";
                    }
                }
            }
        }

        // Trạng thái tương tác của NGƯỜI XEM HIỆN TẠI (IsLiked/IsBookmarked) theo JWT
        if (_currentUser.UserId is Guid viewerId)
        {
            var pageIds = posts.Select(p => p.Id).ToList();
            var likedIds = (await _likeRepository
                    .GetByExpression(l => l.UserId == viewerId && pageIds.Contains(l.PostId), cancellationToken)
                    .Select(l => l.PostId)
                    .ToListAsync(cancellationToken))
                .ToHashSet();
            var bookmarkedIds = (await _bookmarkRepository
                    .GetByExpression(b => b.UserId == viewerId && pageIds.Contains(b.PostId), cancellationToken)
                    .Select(b => b.PostId)
                    .ToListAsync(cancellationToken))
                .ToHashSet();

            foreach (var dto in dtos)
            {
                dto.IsLikedByViewer = likedIds.Contains(dto.Id);
                dto.IsBookmarkedByViewer = bookmarkedIds.Contains(dto.Id);
            }
        }
        var paged = PagedResponse<PostDto>.Create(dtos, request.Page, request.PageSize, totalCount, nextCursor, hasMore);

        return ResponseModel<PagedResponse<PostDto>>.Success(paged);
    }
}
