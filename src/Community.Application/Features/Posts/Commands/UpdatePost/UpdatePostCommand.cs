using Community.Application.Common.Exceptions;
using Community.Application.Common.Models;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Domain.Enums;
using Community.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace Community.Application.Features.Posts.Commands.UpdatePost;

public record UpdatePostCommand : IRequest<ResponseModel<bool>>
{
    public Guid Id { get; init; }
    public string Content { get; init; } = string.Empty;
}

public class UpdatePostCommandValidator : AbstractValidator<UpdatePostCommand>
{
    public UpdatePostCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("PostId is required.");
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Content is required.")
            .MaximumLength(2000).WithMessage("Content must not exceed 2000 characters.");
    }
}

/// <summary>
/// Chỉnh sửa bài viết (FR-16 / UC-16): chỉ TÁC GIẢ mới được sửa bài của mình.
/// Sau khi sửa, bài viết quay về trạng thái chờ kiểm duyệt AI (PendingReview).
/// </summary>
public class UpdatePostCommandHandler : IRequestHandler<UpdatePostCommand, ResponseModel<bool>>
{
    private readonly IPostRepository _postRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IContentModerationService _moderation;

    public UpdatePostCommandHandler(
        IPostRepository postRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IContentModerationService moderation)
    {
        _postRepository = postRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _moderation = moderation;
    }

    public async Task<ResponseModel<bool>> Handle(UpdatePostCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException("Authentication is required.");

        var post = await _postRepository.GetByIdAsync(request.Id, cancellationToken);
        if (post == null)
            throw new NotFoundException(nameof(Post), request.Id);

        // Phân quyền: chỉ tác giả (hoặc Admin) được sửa bài
        if (post.AuthorId != _currentUser.UserId && !_currentUser.IsAdmin)
            throw new ForbiddenException("You can only edit your own posts.");

        // Kiểm duyệt AI trước khi cập nhật (stub hiện tại luôn pass)
        var moderation = await _moderation.CheckAsync(request.Content, cancellationToken);
        if (!moderation.IsValid)
            return ResponseModel<bool>.Error("Content violates community standards (AI moderation).");

        post.Content = request.Content;
        // Nội dung mới cần được kiểm duyệt lại trước khi hiển thị rộng rãi
        post.Status = (int)ContentStatus.Draft;

        await _postRepository.UpdateAsync(post, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        return ResponseModel<bool>.Success(true, "Post updated successfully.");
    }
}
