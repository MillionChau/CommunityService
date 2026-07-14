using Community.Application.Common.Models;
using Community.Domain.Contracts;
using Community.Domain.Entities;
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

public class CreatePostCommandHandler : IRequestHandler<CreatePostCommand, ResponseModel<Guid>>
{
    private readonly IPostRepository _postRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreatePostCommandHandler(IPostRepository postRepository, IUnitOfWork unitOfWork)
    {
        _postRepository = postRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ResponseModel<Guid>> Handle(CreatePostCommand request, CancellationToken cancellationToken)
    {
        var post = new Post
        {
            Id = Guid.NewGuid(),
            Content = request.Content,
            AuthorId = request.AuthorId,
            LikesCount = 0,
            CommentsCount = 0,
            SharesCount = 0,
            ViewsCount = 0
        };

        await _postRepository.AddAsync(post, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        return ResponseModel<Guid>.Success(post.Id);
    }
}
