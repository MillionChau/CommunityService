using Community.Application.Common.Exceptions;
using Community.Application.Common.Models;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace Community.Application.Features.Comments.Commands.ToggleCommentLike;

/// <summary>Kết quả toggle like bình luận.</summary>
public record ToggleCommentLikeResult(Guid CommentId, bool IsLiked, int LikesCount);

public record ToggleCommentLikeCommand(Guid CommentId) : IRequest<ResponseModel<ToggleCommentLikeResult>>;

public class ToggleCommentLikeCommandValidator : AbstractValidator<ToggleCommentLikeCommand>
{
    public ToggleCommentLikeCommandValidator()
    {
        RuleFor(x => x.CommentId).NotEmpty().WithMessage("CommentId is required.");
    }
}

/// <summary>
/// Thích / bỏ thích bình luận (FR-28 / UC-28). Toggle tương tự like bài viết;
/// cặp (CommentId, UserId) được bảo vệ unique index ở tầng DB.
/// </summary>
public class ToggleCommentLikeCommandHandler : IRequestHandler<ToggleCommentLikeCommand, ResponseModel<ToggleCommentLikeResult>>
{
    private readonly ICommentRepository _commentRepository;
    private readonly ICommentLikeRepository _likeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IRealTimeNotifier _notifier;

    public ToggleCommentLikeCommandHandler(
        ICommentRepository commentRepository,
        ICommentLikeRepository likeRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IRealTimeNotifier notifier)
    {
        _commentRepository = commentRepository;
        _likeRepository = likeRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _notifier = notifier;
    }

    public async Task<ResponseModel<ToggleCommentLikeResult>> Handle(ToggleCommentLikeCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException("Authentication is required.");
        var userId = _currentUser.UserId.Value;

        var comment = await _commentRepository.GetByIdAsync(request.CommentId, cancellationToken);
        if (comment == null)
            throw new NotFoundException(nameof(Comment), request.CommentId);

        var existing = await _likeRepository.GetByCommentAndUserAsync(request.CommentId, userId, cancellationToken);
        bool isLiked;
        if (existing == null)
        {
            await _likeRepository.AddAsync(
                new CommentLike { Id = Guid.NewGuid(), CommentId = request.CommentId, UserId = userId },
                cancellationToken);
            comment.LikesCount = (comment.LikesCount ?? 0) + 1;
            isLiked = true;
        }
        else
        {
            await _likeRepository.DeleteAsync(existing.Id, cancellationToken);
            comment.LikesCount = Math.Max((comment.LikesCount ?? 0) - 1, 0);
            isLiked = false;
        }

        await _commentRepository.UpdateAsync(comment, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        var result = new ToggleCommentLikeResult(comment.Id, isLiked, comment.LikesCount ?? 0);

        // Realtime: client đang mở bài viết thấy số like của bình luận cập nhật ngay
        if (comment.PostId.HasValue)
        {
            await _notifier.NotifyPostAsync(comment.PostId.Value, "comment-like-changed",
                new { commentId = comment.Id, isLiked, likesCount = result.LikesCount }, cancellationToken);
        }

        return ResponseModel<ToggleCommentLikeResult>.Success(result,
            isLiked ? "Comment liked." : "Like removed.");
    }
}
