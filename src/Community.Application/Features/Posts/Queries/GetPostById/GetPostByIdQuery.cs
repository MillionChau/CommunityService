using AutoMapper;
using Community.Application.Common.Exceptions;
using Community.Application.Common.Helpers;
using Community.Application.Common.Models;
using Community.Application.DTOs;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Community.Application.Features.Posts.Queries.GetPostById;

public record GetPostByIdQuery(Guid Id) : IRequest<ResponseModel<PostDto>>;

public class GetPostByIdQueryHandler : IRequestHandler<GetPostByIdQuery, ResponseModel<PostDto>>
{
    private readonly IPostRepository _postRepository;
    private readonly IPostLikeRepository _likeRepository;
    private readonly IPostBookmarkRepository _bookmarkRepository;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUser;
    private readonly IUserReputationRepository? _userReputationRepository;

    public GetPostByIdQueryHandler(
        IPostRepository postRepository,
        IPostLikeRepository likeRepository,
        IPostBookmarkRepository bookmarkRepository,
        IMapper mapper,
        ICurrentUserService currentUser,
        IUserReputationRepository? userReputationRepository = null)
    {
        _postRepository = postRepository;
        _likeRepository = likeRepository;
        _bookmarkRepository = bookmarkRepository;
        _mapper = mapper;
        _currentUser = currentUser;
        _userReputationRepository = userReputationRepository;
    }

    public async Task<ResponseModel<PostDto>> Handle(GetPostByIdQuery request, CancellationToken cancellationToken)
    {
        var post = await _postRepository.GetByIdAsync(request.Id, cancellationToken);
        if (post == null)
            throw new NotFoundException(nameof(Post), request.Id);

        // Bảo vệ quyền riêng tư bản nháp (UC-18): Chỉ tác giả hoặc Admin mới có quyền xem bài nháp
        if (post.Status == (int)ContentStatus.Draft)
        {
            if (!_currentUser.IsAuthenticated ||
                (_currentUser.UserId != post.AuthorId && !_currentUser.IsAdmin))
            {
                throw new NotFoundException(nameof(Post), request.Id);
            }
        }

        // Bài viết bị ẩn do vi phạm (hoặc chờ kiểm duyệt): Chỉ tác giả hoặc Admin mới có quyền xem
        if (post.Status == (int)ContentStatus.Hidden || post.IsApproved == 0)
        {
            if (!_currentUser.IsAuthenticated ||
                (_currentUser.UserId != post.AuthorId && !_currentUser.IsAdmin))
            {
                throw new NotFoundException(nameof(Post), request.Id);
            }
        }

        var dto = _mapper.Map<PostDto>(post);

        // Điền thông tin Điểm đóng góp và Cấp bậc (Tier) của tác giả (SPEC-REP-01)
        if (_userReputationRepository != null && post.AuthorId.HasValue)
        {
            var rep = await _userReputationRepository
                .GetByExpression(r => r.UserId == post.AuthorId.Value, cancellationToken)
                .FirstOrDefaultAsync(cancellationToken);

            if (rep != null)
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

        // Trạng thái tương tác của người xem hiện tại (IsLiked/IsBookmarked) theo JWT
        if (_currentUser.UserId is Guid viewerId)
        {
            dto.IsLikedByViewer = await _likeRepository
                .GetByExpression(l => l.UserId == viewerId && l.PostId == post.Id, cancellationToken)
                .AnyAsync(cancellationToken);
            dto.IsBookmarkedByViewer = await _bookmarkRepository
                .GetByExpression(b => b.UserId == viewerId && b.PostId == post.Id, cancellationToken)
                .AnyAsync(cancellationToken);
        }

        return ResponseModel<PostDto>.Success(dto);
    }
}
