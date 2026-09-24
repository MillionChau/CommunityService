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
    private readonly IMapper _mapper;

    public CreateCommentCommandHandler(
        ICommentRepository commentRepository,
        IPostRepository postRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IMapper mapper)
    {
        _commentRepository = commentRepository;
        _postRepository = postRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
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

        // Tăng đếm số comment của bài viết
        post.CommentsCount += 1;
        await _postRepository.UpdateAsync(post, cancellationToken);

        await _commentRepository.AddAsync(comment, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        var dto = _mapper.Map<CommentDto>(comment);
        return ResponseModel<CommentDto>.Success(dto);
    }
}