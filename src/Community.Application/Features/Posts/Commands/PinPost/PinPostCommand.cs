using Community.Application.Common.Exceptions;
using Community.Application.Common.Models;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace Community.Application.Features.Posts.Commands.PinPost;

public record PinPostCommand : IRequest<ResponseModel<bool>>
{
    public Guid Id { get; init; }
    public bool IsPinned { get; init; } = true;
}

public class PinPostCommandValidator : AbstractValidator<PinPostCommand>
{
    public PinPostCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("PostId is required.");
    }
}

/// <summary>
/// Ghim / bỏ ghim bài viết nổi bật (UC-72): dành cho tác giả hoặc quản trị viên (Admin/Mod).
/// </summary>
public class PinPostCommandHandler : IRequestHandler<PinPostCommand, ResponseModel<bool>>
{
    private readonly IPostRepository _postRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public PinPostCommandHandler(
        IPostRepository postRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser)
    {
        _postRepository = postRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ResponseModel<bool>> Handle(PinPostCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException("Authentication is required.");

        var post = await _postRepository.GetByIdAsync(request.Id, cancellationToken);
        if (post == null || post.IsDeleted == 1)
            throw new NotFoundException(nameof(Post), request.Id);

        if (post.AuthorId != _currentUser.UserId && !_currentUser.IsAdmin)
            throw new ForbiddenException("Bạn không có quyền ghim bài viết này.");

        post.IsPinned = request.IsPinned ? 1 : 0;
        post.ModifiedDate = DateTime.UtcNow;

        await _postRepository.UpdateAsync(post, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        string msg = request.IsPinned ? "Ghim bài viết nổi bật thành công." : "Bỏ ghim bài viết thành công.";
        return ResponseModel<bool>.Success(true, msg);
    }
}
