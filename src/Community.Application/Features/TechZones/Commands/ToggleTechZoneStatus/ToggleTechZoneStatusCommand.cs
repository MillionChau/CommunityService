using Community.Application.Common.Exceptions;
using Community.Application.Common.Models;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Domain.Interfaces;
using MediatR;

namespace Community.Application.Features.TechZones.Commands.ToggleTechZoneStatus;

public record ToggleTechZoneStatusCommand(Guid Id) : IRequest<ResponseModel<int>>;

public class ToggleTechZoneStatusCommandHandler : IRequestHandler<ToggleTechZoneStatusCommand, ResponseModel<int>>
{
    private readonly ITechZoneRepository _techZoneRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public ToggleTechZoneStatusCommandHandler(
        ITechZoneRepository techZoneRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser)
    {
        _techZoneRepository = techZoneRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ResponseModel<int>> Handle(ToggleTechZoneStatusCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.IsAdmin)
            throw new ForbiddenException("Only Admin can toggle TechZone status.");

        var zone = await _techZoneRepository.GetByIdAsync(request.Id, cancellationToken);
        if (zone == null)
            throw new NotFoundException(nameof(TechZone), request.Id);

        zone.Status = zone.Status == 1 ? 0 : 1;

        await _techZoneRepository.UpdateAsync(zone, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        var statusMessage = zone.Status == 1 ? "TechZone activated." : "TechZone deactivated.";
        return ResponseModel<int>.Success(zone.Status, statusMessage);
    }
}
