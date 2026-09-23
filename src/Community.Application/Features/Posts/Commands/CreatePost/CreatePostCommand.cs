using Community.Application.Common.Models;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Domain.Enums;
using Community.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace Community.Application.Features.Posts.Commands.CreatePost;

public record CreatePostCommand : IRequest<ResponseModel<Guid>>
{
    public string? Content { get; init; }
    public Guid? AuthorId { get; init; }
}

public class CreatePostCommandValidator : AbstractValidator<CreatePostCommand>
{
    public CreatePostCommandValidator()
    {
        RuleFor(v => v.Content)
            .NotEmpty().WithMessage("Content is required.")
            .MaximumLength(2000).WithMessage("Content must not exceed 2000 characters.");
    }
}

/// <summary>
/// Đăng bài viết mới (FR-15 / UC-15). Trước khi lưu, nội dung được kiểm duyệt
/// bởi IContentModerationService (QualityService): nội dung hợp lệ → Published,
/// vi phạm → Hidden (FLAGGED, chờ Admin xử lý — SRS mục Độ an toàn).
/// AuthorId ưu tiên lấy từ JWT; nếu không có (dev/test) mới dùng body.
/// </summary>
public class CreatePostCommandHandler : IRequestHandler<CreatePostCommand, ResponseModel<Guid>>
{
    private readonly IPostRepository _postRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IContentModerationService _moderation;

    public CreatePostCommandHandler(
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

    public async Task<ResponseModel<Guid>> Handle(CreatePostCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException("Authentication is required.");

        // AuthorId luôn lấy từ JWT token (không tin body — tránh đăng bài hộ người khác)
        var authorId = _currentUser.UserId;

        var content = request.Content ?? string.Empty;

        // Kiểm duyệt nội dung tự động (UC-15: Quality Service kiểm duyệt trước khi hiển thị)
        var moderation = await _moderation.CheckAsync(content, cancellationToken);

        var post = new Post
        {
            Id = Guid.NewGuid(),
            Content = content,
            AuthorId = authorId,
            Status = moderation.IsValid ? (int)ContentStatus.Published : (int)ContentStatus.Hidden,
            LikesCount = 0,
            CommentsCount = 0,
            SharesCount = 0,
            ViewsCount = 0
        };

        await _postRepository.AddAsync(post, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        return ResponseModel<Guid>.Success(post.Id,
            moderation.IsValid ? "Post created successfully." : "Post created but flagged for review.");
    }
}
