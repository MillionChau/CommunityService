using Community.Application.Common.Models;
using Community.Application.DTOs;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Domain.Interfaces;
using AutoMapper;
using FluentValidation;
using MediatR;

namespace Community.Application.Features.Posts.Commands.CreateComment;

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
        RuleFor(x => x.PostId).NotEmpty().WithMessage("PostId is required.");
        RuleFor(x => x.Content).NotEmpty().WithMessage("Content is required.");
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

    public CreateCommentCommandHandler(
        ICommentRepository commentRepository,
        IPostRepository postRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IRealTimeNotifier notifier,
        INotificationClient notificationClient,
        IMapper mapper)
    {
        _commentRepository = commentRepository;
        _postRepository = postRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _notifier = notifier;
        _notificationClient = notificationClient;
        _mapper = mapper;
    }

    public async Task<ResponseModel<CommentDto>> Handle(CreateCommentCommand request, CancellationToken cancellationToken)
    {
        // AuthorId luôn lấy từ JWT (không tin body)
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException("Authentication is required.");

        var post = await _postRepository.GetByIdAsync(request.PostId, cancellationToken);
        if (post == null)
            throw new Common.Exceptions.NotFoundException("Post", request.PostId);

        var comment = new Comment
        {
            Id = Guid.NewGuid(),
            PostId = request.PostId,
            AuthorId = _currentUser.UserId,
            Content = request.Content
        };

        // Tăng đếm số comment của bài viết (int? — phải coalesce null, += trên null vẫn là null)
        post.CommentsCount = (post.CommentsCount ?? 0) + 1;
        await _postRepository.UpdateAsync(post, cancellationToken);

        await _commentRepository.AddAsync(comment, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        var dto = _mapper.Map<CommentDto>(comment);

        // Realtime: đẩy bình luận mới cho mọi client đang mở bài viết này
        await _notifier.NotifyPostAsync(request.PostId, "comment-created", dto, cancellationToken);

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

        return ResponseModel<CommentDto>.Success(dto);
    }
}