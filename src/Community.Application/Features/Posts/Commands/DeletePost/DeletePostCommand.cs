using Community.Application.Common.Exceptions;
using Community.Application.Common.Models;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Domain.Enums;
using Community.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace Community.Application.Features.Posts.Commands.DeletePost;

public record DeletePostCommand : IRequest<ResponseModel<bool>>
{
    public Guid Id { get; init; }
}

public class DeletePostCommandValidator : AbstractValidator<DeletePostCommand>
{
    public DeletePostCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("PostId is required.");
    }
}

/// <summary>
/// Xóa bài viết (FR-17 / UC-17): soft delete — đặt Status = Deleted,
/// chỉ tác giả hoặc Admin mới được xóa.
/// </summary>
public class DeletePostCommandHandler : IRequestHandler<DeletePostCommand, ResponseModel<bool>>
{
    private readonly IPostRepository _postRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IRealTimeNotifier _notifier;

    public DeletePostCommandHandler(
        IPostRepository postRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IRealTimeNotifier notifier)
    {
        _postRepository = postRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _notifier = notifier;
    }

    public async Task<ResponseModel<bool>> Handle(DeletePostCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException("Authentication is required.");

        var post = await _postRepository.GetByIdAsync(request.Id, cancellationToken);
        if (post == null)
            throw new NotFoundException(nameof(Post), request.Id);

        // Phân quyền: tác giả hoặc Admin
        if (post.AuthorId != _currentUser.UserId && !_currentUser.IsAdmin)
            throw new ForbiddenException("You can only delete your own posts.");

        // Soft delete theo SRS UC-17: "cập nhật trạng thái", không xóa vật lý
        post.Status = (int)ContentStatus.Deleted;

        await _postRepository.UpdateAsync(post, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        // Realtime: bài bị xóa — cả feed lẫn trang chi tiết cần biết để gỡ khỏi UI
        await _notifier.NotifyFeedAsync("post-deleted", new { postId = post.Id }, cancellationToken);
        await _notifier.NotifyPostAsync(post.Id, "post-deleted", new { postId = post.Id }, cancellationToken);

        return ResponseModel<bool>.Success(true, "Post deleted successfully.");
    }
}
