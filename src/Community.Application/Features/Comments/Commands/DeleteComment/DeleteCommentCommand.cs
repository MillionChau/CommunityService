using Community.Application.Common.Exceptions;
using Community.Application.Common.Models;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Domain.Enums;
using Community.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace Community.Application.Features.Comments.Commands.DeleteComment;

public record DeleteCommentCommand : IRequest<ResponseModel<bool>>
{
    public Guid Id { get; init; }
}

public class DeleteCommentCommandValidator : AbstractValidator<DeleteCommentCommand>
{
    public DeleteCommentCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("CommentId is required.");
    }
}

/// <summary>
/// Xóa bình luận (FR-27 / UC-27): soft delete, chỉ tác giả bình luận hoặc Admin.
/// Đồng thời giảm CommentsCount của bài viết cha để số đếm luôn nhất quán.
/// </summary>
public class DeleteCommentCommandHandler : IRequestHandler<DeleteCommentCommand, ResponseModel<bool>>
{
    private readonly ICommentRepository _commentRepository;
    private readonly IPostRepository _postRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IRealTimeNotifier _notifier;

    public DeleteCommentCommandHandler(
        ICommentRepository commentRepository,
        IPostRepository postRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IRealTimeNotifier notifier)
    {
        _commentRepository = commentRepository;
        _postRepository = postRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _notifier = notifier;
    }

    public async Task<ResponseModel<bool>> Handle(DeleteCommentCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException("Authentication is required.");

        var comment = await _commentRepository.GetByIdAsync(request.Id, cancellationToken);
        if (comment == null)
            throw new NotFoundException(nameof(Comment), request.Id);

        // Phân quyền: tác giả bình luận hoặc Admin
        if (comment.AuthorId != _currentUser.UserId && !_currentUser.IsAdmin)
            throw new ForbiddenException("You can only delete your own comments.");

        comment.Status = (int)ContentStatus.Deleted;
        await _commentRepository.UpdateAsync(comment, cancellationToken);

        // Giảm đếm comment của bài viết (bù lại tăng khi CreateComment; int? phải coalesce)
        Guid postId = comment.PostId ?? Guid.Empty;
        if (postId != Guid.Empty)
        {
            var post = await _postRepository.GetByIdAsync(postId, cancellationToken);
            if (post != null)
            {
                post.CommentsCount = Math.Max((post.CommentsCount ?? 0) - 1, 0);
                await _postRepository.UpdateAsync(post, cancellationToken);
            }
        }

        await _unitOfWork.SaveAsync(cancellationToken);

        // Realtime: client đang mở bài viết gỡ bình luận này khỏi UI ngay
        await _notifier.NotifyPostAsync(postId, "comment-deleted", new { commentId = comment.Id }, cancellationToken);

        return ResponseModel<bool>.Success(true, "Comment deleted successfully.");
    }
}
