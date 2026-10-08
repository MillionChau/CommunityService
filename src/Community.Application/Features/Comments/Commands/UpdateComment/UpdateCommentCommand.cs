using Community.Application.Common.Exceptions;
using Community.Application.Common.Models;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace Community.Application.Features.Comments.Commands.UpdateComment;

public record UpdateCommentCommand : IRequest<ResponseModel<bool>>
{
    public Guid Id { get; init; }
    public string Content { get; init; } = string.Empty;
}

public class UpdateCommentCommandValidator : AbstractValidator<UpdateCommentCommand>
{
    public UpdateCommentCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("CommentId is required.");
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Content is required.")
            .MaximumLength(1000).WithMessage("Content must not exceed 1000 characters.");
    }
}

/// <summary>
/// Chỉnh sửa bình luận (UC-33): chỉ tác giả bình luận mới có quyền sửa.
/// </summary>
public class UpdateCommentCommandHandler : IRequestHandler<UpdateCommentCommand, ResponseModel<bool>>
{
    private readonly ICommentRepository _commentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public UpdateCommentCommandHandler(
        ICommentRepository commentRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser)
    {
        _commentRepository = commentRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ResponseModel<bool>> Handle(UpdateCommentCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException("Authentication is required.");

        var comment = await _commentRepository.GetByIdAsync(request.Id, cancellationToken);
        if (comment == null || comment.IsDeleted == 1)
            throw new NotFoundException(nameof(Comment), request.Id);

        if (comment.AuthorId != _currentUser.UserId)
            throw new ForbiddenException("Chỉ tác giả mới có quyền chỉnh sửa bình luận này.");

        comment.Content = request.Content.Trim();
        comment.ModifiedDate = DateTime.UtcNow;

        await _commentRepository.UpdateAsync(comment, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        return ResponseModel<bool>.Success(true, "Cập nhật bình luận thành công.");
    }
}
