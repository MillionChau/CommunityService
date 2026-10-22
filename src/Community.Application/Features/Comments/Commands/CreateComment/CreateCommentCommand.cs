using AutoMapper;
using Community.Application.Common.Models;
using Community.Application.DTOs;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Domain.Enums;
using Community.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace Community.Application.Features.Comments.Commands.CreateComment;

public record CreateCommentCommand : IRequest<ResponseModel<CommentDto>>
{
    public Guid PostId { get; init; }
    public string Content { get; init; } = string.Empty;
    public Guid? ParentCommentId { get; init; }
}

public class CreateCommentCommandValidator : AbstractValidator<CreateCommentCommand>
{
    public CreateCommentCommandValidator()
    {
        RuleFor(v => v.PostId)
            .NotEmpty().WithMessage("PostId is required.");

        RuleFor(v => v.Content)
            .NotEmpty().WithMessage("Content is required.")
            .MaximumLength(1000).WithMessage("Content must not exceed 1000 characters.");
    }
}

public class CreateCommentCommandHandler : IRequestHandler<CreateCommentCommand, ResponseModel<CommentDto>>
{
    private readonly ICommentRepository _commentRepository;
    private readonly IPostRepository _postRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IRealTimeNotifier _notifier;
    private readonly INotificationClient _notificationClient;
    private readonly IMapper _mapper;
    private readonly IContentModerationService? _moderation;
    private readonly IEventPublisher? _eventPublisher;
    private readonly IReputationService? _reputationService;

    public CreateCommentCommandHandler(
        ICommentRepository commentRepository,
        IPostRepository postRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IRealTimeNotifier notifier,
        INotificationClient notificationClient,
        IMapper mapper,
        IContentModerationService? moderation = null,
        IEventPublisher? eventPublisher = null,
        IReputationService? reputationService = null)
    {
        _commentRepository = commentRepository;
        _postRepository = postRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _notifier = notifier;
        _notificationClient = notificationClient;
        _mapper = mapper;
        _moderation = moderation;
        _eventPublisher = eventPublisher;
        _reputationService = reputationService;
    }

    public async Task<ResponseModel<CommentDto>> Handle(CreateCommentCommand request, CancellationToken cancellationToken)
    {
        // AuthorId luôn lấy từ JWT (không tin body)
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException("Authentication is required.");

        var authorId = _currentUser.UserId.Value;

        // SPEC-REP: Kiểm tra quyền hạn bình luận theo điểm tín nhiệm TSS & Chế tài xử phạt
        if (_reputationService != null)
        {
            var rep = await _reputationService.GetUserReputationAsync(authorId, cancellationToken);
            if (!rep.CanComment)
            {
                throw new InvalidOperationException($"Tài khoản của bạn đang bị giới hạn bình luận do vi phạm tiêu chuẩn cộng đồng (Chế tài: {rep.SanctionReason ?? "Hạn chế tương tác"}).");
            }
        }

        var post = await _postRepository.GetByIdAsync(request.PostId, cancellationToken);
        if (post == null)
            throw new Common.Exceptions.NotFoundException("Post", request.PostId);

        // Kiểm duyệt bình luận bằng AI (QualityService)
        var isFlagged = false;
        if (_moderation != null)
        {
            var moderation = await _moderation.CheckAsync(request.Content, "comment", cancellationToken);
            if (!moderation.IsValid)
            {
                isFlagged = true;
            }
        }

        var comment = new Comment
        {
            Id = Guid.NewGuid(),
            PostId = request.PostId,
            AuthorId = _currentUser.UserId,
            Content = request.Content,
            ParentCommentId = request.ParentCommentId,
            Status = isFlagged ? (int)ContentStatus.Hidden : (int)ContentStatus.Published,
            IsApproved = isFlagged ? 0 : 1
        };

        if (isFlagged)
        {
            await _commentRepository.AddAsync(comment, cancellationToken);
            await _unitOfWork.SaveAsync(cancellationToken);

            // SPEC-REP: Trừ điểm vi phạm tiêu chuẩn cộng đồng (-15 PRP, -10 TSS)
            if (_reputationService != null)
            {
                await _reputationService.DeductPointsAsync(
                    authorId,
                    "COMMENT_FLAGGED_AI",
                    pointsPenalty: 15,
                    tssPenalty: 10,
                    sourceId: comment.Id,
                    reason: "Nội dung bình luận vi phạm tiêu chuẩn cộng đồng (Hệ thống AI kiểm duyệt)",
                    cancellationToken);
            }

            var flaggedDto = _mapper.Map<CommentDto>(comment);
            return ResponseModel<CommentDto>.Success(flaggedDto, "Comment created but flagged for review.");
        }

        // Nếu là reply (UC-25): tăng đếm số replies của comment cha
        if (request.ParentCommentId is { } parentCommentId)
        {
            var parentComment = await _commentRepository.GetByIdAsync(parentCommentId, cancellationToken);
            if (parentComment != null)
            {
                parentComment.RepliesCount = (parentComment.RepliesCount ?? 0) + 1;
                await _commentRepository.UpdateAsync(parentComment, cancellationToken);
            }
        }

        // Tăng đếm số comment của bài viết
        post.CommentsCount = (post.CommentsCount ?? 0) + 1;
        await _postRepository.UpdateAsync(post, cancellationToken);

        await _commentRepository.AddAsync(comment, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        // SPEC-REP: Bình luận đóng góp thành công (+3 PRP, +3 PCS)
        if (_reputationService != null)
        {
            await _reputationService.AddPointsAsync(
                authorId,
                "COMMENT_CREATED",
                pointsDelta: 3,
                tssDelta: 0,
                sourceId: comment.Id,
                reason: "Bình luận thảo luận kỹ thuật",
                cancellationToken);
        }

        var dto = _mapper.Map<CommentDto>(comment);

        // Realtime: đẩy bình luận mới cho mọi client đang mở bài viết này
        await _notifier.NotifyPostAsync(request.PostId, "comment-created", dto, cancellationToken);

        // RabbitMQ: Publish CommentCreatedEvent to devradar.events (routing key comment.created)
        if (_eventPublisher != null)
        {
            await _eventPublisher.PublishAsync("comment.created", new
            {
                commentId = comment.Id,
                postId = post.Id,
                authorId = comment.AuthorId,
                content = comment.Content,
                parentCommentId = comment.ParentCommentId,
                createdDate = comment.CreatedDate
            }, cancellationToken);
        }

        // ===== Thông báo tương tác trực tiếp (UC-19: Notification Service) =====
        var actorName = _currentUser.UserName ?? "Ai đó";
        var notifications = new List<NotificationOutbound>();

        // 1. Reply comment → ưu tiên thông báo cho tác giả comment cha (FR-25)
        if (request.ParentCommentId is { } parentId)
        {
            var parentComment = _commentRepository.GetByExpression(c => c.Id == parentId)
                .FirstOrDefault();
            if (parentComment?.AuthorId is { } parentAuthorId && parentAuthorId != _currentUser.UserId)
            {
                notifications.Add(new NotificationOutbound
                {
                    RecipientId = parentAuthorId,
                    Type = NotificationTypes.CommentReplied,
                    Content = $"{actorName} đã trả lời bình luận của bạn.",
                    LinkUrl = $"/posts/{post.Id}#comment-{parentId}",
                    RelatedEntityId = parentId,
                    RelatedPostId = post.Id,
                    ActorId = _currentUser.UserId!.Value,
                    ActorName = _currentUser.UserName
                });
            }
        }

        // 2. Comment bài viết → thông báo cho tác giả bài (FR-24), trừ khi chính tác giả tự bình luận
        if (post.AuthorId is { } postAuthorId
            && postAuthorId != _currentUser.UserId
            && !notifications.Any(n => n.RecipientId == postAuthorId))
        {
            notifications.Add(new NotificationOutbound
            {
                RecipientId = postAuthorId,
                Type = NotificationTypes.PostCommented,
                Content = $"{actorName} đã bình luận về bài viết của bạn.",
                LinkUrl = $"/posts/{post.Id}#comment-{comment.Id}",
                RelatedEntityId = comment.Id,
                RelatedPostId = post.Id,
                ActorId = _currentUser.UserId!.Value,
                ActorName = _currentUser.UserName
            });
        }

        if (notifications.Count > 0)
            await _notificationClient.SendManyAsync(notifications, cancellationToken);

        return ResponseModel<CommentDto>.Success(dto, "Comment created successfully.");
    }
}
