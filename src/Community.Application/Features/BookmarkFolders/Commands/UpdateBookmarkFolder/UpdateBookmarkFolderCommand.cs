using Community.Application.Common.Exceptions;
using Community.Application.Common.Models;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace Community.Application.Features.BookmarkFolders.Commands.UpdateBookmarkFolder;

public record UpdateBookmarkFolderCommand : IRequest<ResponseModel<bool>>
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? ColorCode { get; init; }
}

public class UpdateBookmarkFolderCommandValidator : AbstractValidator<UpdateBookmarkFolderCommand>
{
    public UpdateBookmarkFolderCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Id is required.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Folder name is required.")
            .MaximumLength(100).WithMessage("Folder name must not exceed 100 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters.");
    }
}

public class UpdateBookmarkFolderCommandHandler : IRequestHandler<UpdateBookmarkFolderCommand, ResponseModel<bool>>
{
    private readonly IBookmarkFolderRepository _folderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public UpdateBookmarkFolderCommandHandler(
        IBookmarkFolderRepository folderRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser)
    {
        _folderRepository = folderRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ResponseModel<bool>> Handle(UpdateBookmarkFolderCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException("Authentication is required.");

        var userId = _currentUser.UserId.Value;

        var folder = await _folderRepository.GetByIdAsync(request.Id, cancellationToken);
        if (folder == null)
            throw new NotFoundException(nameof(BookmarkFolder), request.Id);

        if (folder.UserId != userId && !_currentUser.IsAdmin)
            throw new ForbiddenException("You can only update your own bookmark folders.");

        folder.Name = request.Name.Trim();
        folder.Description = request.Description?.Trim();
        folder.ColorCode = request.ColorCode?.Trim();

        await _folderRepository.UpdateAsync(folder, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        return ResponseModel<bool>.Success(true, "Bookmark folder updated successfully.");
    }
}
