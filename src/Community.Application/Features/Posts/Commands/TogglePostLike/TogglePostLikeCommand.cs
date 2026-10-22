using Community.Application.Common.Exceptions;
using Community.Application.Common.Models;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace Community.Application.Features.Posts.Commands.TogglePostLike;

/// <summary>Kết quả toggle like trả về cho client (và broadcast realtime).</summary>
public record ToggleLikeResult(Guid PostId, bool IsLiked, int LikesCount);

public record TogglePostLikeCommand(Guid PostId) : IRequest<ResponseModel<ToggleLikeResult>>;

public class TogglePostLikeCommandValidator : AbstractValidator<TogglePostLikeCommand>
{
    public TogglePostLikeCommandValidator()
    {
        RuleFor(x => x.PostId).NotEmpty().WithMessage("PostId is required.");
    }
}

/// <summary>
/// Thích / bỏ thích bài viết (FR-19 / UC-19). Toggle: nếu chưa thích → thêm
/// PostLike + tăng LikesCount; nếu đã thích → xóa + giảm. Cặp (PostId, UserId)
/// được bảo vệ unique index ở tầng DB nên không thể like trùng kể cả race condition.
/// UC-19 đề cập Notification Service (đang ngoài phạm vi — sẽ gửi thông báo ở phiên sau).
/// </summary>
public class TogglePostLikeCommandHandler : IRequestHandler<TogglePostLikeCommand, ResponseModel<ToggleLikeResult>>
{
    private readonly IPostRepository _postRepository;
    private readonly IPostLikeRepository _likeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IRealTimeNotifier _notifier;
    private readonly INotificationClient _notificationClient;
    private readonly IReputationService? _reputationService;

    public TogglePostLikeCommandHandler(
        IPostRepository postRepository,
        IPostLikeRepository likeRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IRealTimeNotifier notifier,
        INotificationClient notificationClient,
        IReputationService? reputationService = null)
    {
        _postRepository = postRepository;
        _likeRepository = likeRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _notifier = notifier;
        _notificationClient = notificationClient;
        _reputationService = reputationService;
    }

    public async Task<ResponseModel<ToggleLikeResult>> Handle(TogglePostLikeCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException("Authentication is required.");
        var userId = _currentUser.UserId.Value;

        var post = await _postRepository.GetByIdAsync(request.PostId, cancellationToken);
        if (post == null)
            throw new NotFoundException(nameof(Post), request.PostId);

        var existing = await _likeRepository.GetByPostAndUserAsync(request.PostId, userId, cancellationToken);
        bool isLiked;
        if (existing == null)
        {
            await _likeRepository.AddAsync(new PostLike { Id = Guid.NewGuid(), PostId = request.PostId, UserId = userId },
                cancellationToken);
            post.LikesCount = (post.LikesCount ?? 0) + 1;
            isLiked = true;
        }
        else
        {
            await _likeRepository.DeleteAsync(existing.Id, cancellationToken);
            post.LikesCount = Math.Max((post.LikesCount ?? 0) - 1, 0);
            isLiked = false;
        }

        await _postRepository.UpdateAsync(post, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        // SPEC-REP: Cộng/Trừ điểm uy tín khi bài viết được Like/Unlike (+5 PRP, +5 PCS)
        if (post.AuthorId is { } authorId && authorId != userId && _reputationService != null)
        {
            if (isLiked)
            {
                await _reputationService.AddPointsAsync(
                    authorId,
                    "POST_LIKED",
                    pointsDelta: 5,
                    tssDelta: 0,
                    sourceId: post.Id,
                    reason: "Bài viết nhận được lượt thích từ cộng đồng",
                    cancellationToken);
            }
            else
            {
                await _reputationService.DeductPointsAsync(
                    authorId,
                    "POST_UNLIKED",
                    pointsPenalty: 5,
                    tssPenalty: 0,
                    sourceId: post.Id,
                    reason: "Bài viết bị hủy lượt thích",
                    cancellationToken);
            }
        }

        var result = new ToggleLikeResult(post.Id, isLiked, post.LikesCount ?? 0);

        // Realtime: cả feed lẫn trang chi tiết hiển thị số like mới
        await _notifier.NotifyFeedAsync("post-like-changed",
            new { postId = post.Id, likesCount = result.LikesCount }, cancellationToken);
        await _notifier.NotifyPostAsync(post.Id, "post-like-changed",
            new { postId = post.Id, isLiked, likesCount = result.LikesCount }, cancellationToken);

        // Thông báo cho tác giả bài viết khi được like (UC-19, FR-29) — bỏ qua like lại của chính tác giả
        if (isLiked && post.AuthorId is { } postAuthorId && postAuthorId != userId)
        {
            await _notificationClient.SendAsync(new NotificationOutbound
            {
                RecipientId = postAuthorId,
                Type = NotificationTypes.PostLiked,
                Content = $"{_currentUser.UserName ?? "Ai đó"} đã thích bài viết của bạn.",
                LinkUrl = $"/posts/{post.Id}",
                RelatedEntityId = post.Id,
                RelatedPostId = post.Id,
                ActorId = userId,
                ActorName = _currentUser.UserName
            }, cancellationToken);
        }

        return ResponseModel<ToggleLikeResult>.Success(result,
            isLiked ? "Post liked." : "Like removed.");
    }
}
