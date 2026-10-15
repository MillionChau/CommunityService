using Community.Application.Common.Exceptions;
using Community.Application.Common.Models;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace Community.Application.Features.TechZones.Commands.UpdateTechZoneTags;

public record UpdateTechZoneTagsCommand(Guid Id, string Tags) : IRequest<ResponseModel<bool>>;

public class UpdateTechZoneTagsCommandValidator : AbstractValidator<UpdateTechZoneTagsCommand>
{
    public UpdateTechZoneTagsCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Id is required.");
        RuleFor(x => x.Tags).MaximumLength(500).WithMessage("Tags must not exceed 500 characters.");
    }
}

public class UpdateTechZoneTagsCommandHandler : IRequestHandler<UpdateTechZoneTagsCommand, ResponseModel<bool>>
{
    private readonly ITechZoneRepository _techZoneRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public UpdateTechZoneTagsCommandHandler(
        ITechZoneRepository techZoneRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser)
    {
        _techZoneRepository = techZoneRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ResponseModel<bool>> Handle(UpdateTechZoneTagsCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.IsAdmin)
            throw new ForbiddenException("Only Admin can update TechZone tags.");

        var zone = await _techZoneRepository.GetByIdAsync(request.Id, cancellationToken);
        if (zone == null)
            throw new NotFoundException(nameof(TechZone), request.Id);

        zone.Tags = request.Tags?.Trim();

        await _techZoneRepository.UpdateAsync(zone, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        return ResponseModel<bool>.Success(true, "TechZone tags updated successfully.");
    }
}
