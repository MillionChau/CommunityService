using Community.Application.Common.Exceptions;
using Community.Application.Common.Models;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace Community.Application.Features.TechZones.Commands.CreateTechZone;

public record CreateTechZoneCommand : IRequest<ResponseModel<Guid>>
{
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? IconUrl { get; init; }
    public string? ColorCode { get; init; }
    public string? Tags { get; init; }
    public int DisplayOrder { get; init; } = 0;
}

public class CreateTechZoneCommandValidator : AbstractValidator<CreateTechZoneCommand>
{
    public CreateTechZoneCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Slug is required.")
            .MaximumLength(100).WithMessage("Slug must not exceed 100 characters.")
            .Matches("^[a-z0-9-]+$").WithMessage("Slug can only contain lowercase letters, numbers, and hyphens.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters.");
    }
}

public class CreateTechZoneCommandHandler : IRequestHandler<CreateTechZoneCommand, ResponseModel<Guid>>
{
    private readonly ITechZoneRepository _techZoneRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public CreateTechZoneCommandHandler(
        ITechZoneRepository techZoneRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser)
    {
        _techZoneRepository = techZoneRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ResponseModel<Guid>> Handle(CreateTechZoneCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.IsAdmin)
            throw new ForbiddenException("Only Admin can create TechZones.");

        var normalizedSlug = request.Slug.Trim().ToLower();
        if (await _techZoneRepository.SlugExistsAsync(normalizedSlug, cancellationToken: cancellationToken))
            throw new Community.Application.Common.Exceptions.ValidationException(new List<FluentValidation.Results.ValidationFailure>
            {
                new(nameof(request.Slug), $"TechZone with slug '{normalizedSlug}' already exists.")
            });

        var zone = new TechZone
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Slug = normalizedSlug,
            Description = request.Description?.Trim(),
            IconUrl = request.IconUrl?.Trim(),
            ColorCode = request.ColorCode?.Trim(),
            Tags = request.Tags?.Trim(),
            DisplayOrder = request.DisplayOrder,
            Status = 1,
            PostsCount = 0,
            FollowersCount = 0
        };

        await _techZoneRepository.AddAsync(zone, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        return ResponseModel<Guid>.Success(zone.Id, "TechZone created successfully.");
    }
}
