using Community.Application.Common.Models;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace Community.Application.Features.BookmarkFolders.Commands.CreateBookmarkFolder;

public record CreateBookmarkFolderCommand : IRequest<ResponseModel<Guid>>
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? ColorCode { get; init; }
}

public class CreateBookmarkFolderCommandValidator : AbstractValidator<CreateBookmarkFolderCommand>
{
    public CreateBookmarkFolderCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Folder name is required.")
            .MaximumLength(100).WithMessage("Folder name must not exceed 100 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters.");
    }
}

public class CreateBookmarkFolderCommandHandler : IRequestHandler<CreateBookmarkFolderCommand, ResponseModel<Guid>>
{
    private readonly IBookmarkFolderRepository _folderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public CreateBookmarkFolderCommandHandler(
        IBookmarkFolderRepository folderRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser)
    {
        _folderRepository = folderRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ResponseModel<Guid>> Handle(CreateBookmarkFolderCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException("Authentication is required.");

        var userId = _currentUser.UserId.Value;

        var folder = new BookmarkFolder
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            ColorCode = request.ColorCode?.Trim(),
            UserId = userId,
            ItemsCount = 0
        };

        await _folderRepository.AddAsync(folder, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        return ResponseModel<Guid>.Success(folder.Id, "Bookmark folder created successfully.");
    }
}
