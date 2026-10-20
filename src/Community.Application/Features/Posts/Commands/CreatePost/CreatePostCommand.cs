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
    /// <summary>
    /// Cờ lưu nháp bài viết (UC-18). Mặc định false (đăng bài công khai).
    /// </summary>
    public bool IsDraft { get; init; } = false;
    /// <summary>TechZone liên kết (UC-14). Null = không gán.</summary>
    public Guid? TechZoneId { get; init; }
    /// <summary>Tag công nghệ (UC-13). Ví dụ: "dotnet,csharp".</summary>
    public string? Tags { get; init; }
}

public class CreatePostCommandValidator : AbstractValidator<CreatePostCommand>
{
    public CreatePostCommandValidator()
    {
        // Khi đăng bài công khai (IsDraft = false), Content bắt buộc không được rỗng
        RuleFor(v => v.Content)
            .NotEmpty()
            .When(v => !v.IsDraft)
            .WithMessage("Content is required when publishing a post.");

        RuleFor(v => v.Content)
            .MaximumLength(2000)
            .WithMessage("Content must not exceed 2000 characters.");

        RuleFor(v => v.Tags)
            .MaximumLength(500)
            .WithMessage("Tags must not exceed 500 characters.");
    }
}

/// <summary>
/// Đăng bài viết mới (FR-15 / UC-15) hoặc Lưu bản nháp (UC-18).
/// Khi IsDraft = true: lưu bài viết ở trạng thái Draft, bỏ qua kiểm duyệt AI và không phát SignalR feed.
/// Khi IsDraft = false: nội dung được kiểm duyệt bởi IContentModerationService (QualityService):
/// hợp lệ → Published, vi phạm → Hidden (FLAGGED, chờ Admin xử lý).
/// AuthorId luôn lấy từ JWT.
/// </summary>
public class CreatePostCommandHandler : IRequestHandler<CreatePostCommand, ResponseModel<Guid>>
{
    private readonly IPostRepository _postRepository;
    private readonly ITechZoneRepository? _techZoneRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IContentModerationService _moderation;
    private readonly IRealTimeNotifier _notifier;

    public CreatePostCommandHandler(
        IPostRepository postRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IContentModerationService moderation,
        IRealTimeNotifier notifier,
        ITechZoneRepository? techZoneRepository = null)
    {
        _postRepository = postRepository;
        _techZoneRepository = techZoneRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _moderation = moderation;
        _notifier = notifier;
    }

    public async Task<ResponseModel<Guid>> Handle(CreatePostCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException("Authentication is required.");

        // AuthorId luôn lấy từ JWT token (không tin body — tránh đăng bài hộ người khác)
        var authorId = _currentUser.UserId;

        var content = request.Content ?? string.Empty;

        // UC-18: Xử lý lưu bản nháp
        if (request.IsDraft)
        {
            var draftPost = new Post
            {
                Id = Guid.NewGuid(),
                Content = content,
                AuthorId = authorId,
                TechZoneId = request.TechZoneId,
                Tags = request.Tags?.Trim(),
                Status = (int)ContentStatus.Draft,
                LikesCount = 0,
                CommentsCount = 0,
                SharesCount = 0,
                ViewsCount = 0
            };

            await _postRepository.AddAsync(draftPost, cancellationToken);
            await _unitOfWork.SaveAsync(cancellationToken);

            // Bản nháp là riêng tư của tác giả: KHÔNG phát SignalR ra public feed
            return ResponseModel<Guid>.Success(draftPost.Id, "Draft saved successfully.");
        }

        // Kiểm duyệt nội dung tự động (UC-15: Quality Service kiểm duyệt trước khi hiển thị)
        var moderation = await _moderation.CheckAsync(content, cancellationToken);

        if (!moderation.IsValid)
        {
            var reason = moderation.Toxicity == "toxic" || moderation.Toxicity == "suspicious"
                ? $"Nội dung bài viết bị từ chối do vi phạm tiêu chuẩn kiểm duyệt cộng đồng (Toxicity: {moderation.Toxicity}). Vui lòng điều chỉnh lại câu từ."
                : $"Nội dung bài viết không thuộc lĩnh vực công nghệ thông tin (non-it, Điểm chất lượng: {moderation.QualityScore}/100).";

            return ResponseModel<Guid>.Error(reason);
        }

        var post = new Post
        {
            Id = Guid.NewGuid(),
            Content = content,
            AuthorId = authorId,
            TechZoneId = request.TechZoneId,
            Tags = request.Tags?.Trim(),
            Status = (int)ContentStatus.Published,
            IsApproved = 1,
            LikesCount = 0,
            CommentsCount = 0,
            SharesCount = 0,
            ViewsCount = 0
        };

        if (request.TechZoneId.HasValue && _techZoneRepository != null)
        {
            var zone = await _techZoneRepository.GetByIdAsync(request.TechZoneId.Value, cancellationToken);
            if (zone != null)
            {
                zone.PostsCount += 1;
                await _techZoneRepository.UpdateAsync(zone, cancellationToken);
            }
        }

        await _postRepository.AddAsync(post, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        // Realtime: đẩy bài mới lên mọi client đang xem bảng tin
        await _notifier.NotifyFeedAsync("post-created", new
        {
            postId = post.Id,
            authorId = post.AuthorId,
            content = post.Content,
            status = post.Status,
            likesCount = post.LikesCount,
            commentsCount = post.CommentsCount,
            sharesCount = post.SharesCount,
            bookmarksCount = post.BookmarksCount,
            createdDate = post.CreatedDate
        }, cancellationToken);

        return ResponseModel<Guid>.Success(post.Id, "Post created successfully.");
    }
}
