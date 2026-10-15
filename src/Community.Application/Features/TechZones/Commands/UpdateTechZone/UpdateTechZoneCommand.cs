using Community.Application.Common.Exceptions;
using Community.Application.Common.Models;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace Community.Application.Features.TechZones.Commands.UpdateTechZone;

public record UpdateTechZoneCommand : IRequest<ResponseModel<bool>>
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? IconUrl { get; init; }
    public string? ColorCode { get; init; }
    public int DisplayOrder { get; init; } = 0;
}

public class UpdateTechZoneCommandValidator : AbstractValidator<UpdateTechZoneCommand>
{
    public UpdateTechZoneCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Id is required.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters.");
    }
}

public class UpdateTechZoneCommandHandler : IRequestHandler<UpdateTechZoneCommand, ResponseModel<bool>>
{
    private readonly ITechZoneRepository _techZoneRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public UpdateTechZoneCommandHandler(
        ITechZoneRepository techZoneRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser)
    {
        _techZoneRepository = techZoneRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ResponseModel<bool>> Handle(UpdateTechZoneCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.IsAdmin)
            throw new ForbiddenException("Only Admin can update TechZones.");

        var zone = await _techZoneRepository.GetByIdAsync(request.Id, cancellationToken);
        if (zone == null)
            throw new NotFoundException(nameof(TechZone), request.Id);

        zone.Name = request.Name.Trim();
        zone.Description = request.Description?.Trim();
        zone.IconUrl = request.IconUrl?.Trim();
        zone.ColorCode = request.ColorCode?.Trim();
        zone.DisplayOrder = request.DisplayOrder;

        await _techZoneRepository.UpdateAsync(zone, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        return ResponseModel<bool>.Success(true, "TechZone updated successfully.");
    }
}
