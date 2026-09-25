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

    public TogglePostLikeCommandHandler(
        IPostRepository postRepository,
        IPostLikeRepository likeRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IRealTimeNotifier notifier)
    {
        _postRepository = postRepository;
        _likeRepository = likeRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _notifier = notifier;
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

        var result = new ToggleLikeResult(post.Id, isLiked, post.LikesCount ?? 0);

        // Realtime: cả feed lẫn trang chi tiết hiển thị số like mới
        await _notifier.NotifyFeedAsync("post-like-changed",
            new { postId = post.Id, likesCount = result.LikesCount }, cancellationToken);
        await _notifier.NotifyPostAsync(post.Id, "post-like-changed",
            new { postId = post.Id, isLiked, likesCount = result.LikesCount }, cancellationToken);

        return ResponseModel<ToggleLikeResult>.Success(result,
            isLiked ? "Post liked." : "Like removed.");
    }
}
