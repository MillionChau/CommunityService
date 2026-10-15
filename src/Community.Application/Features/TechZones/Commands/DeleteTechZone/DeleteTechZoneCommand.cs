using Community.Application.Common.Exceptions;
using Community.Application.Common.Models;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Community.Application.Features.TechZones.Commands.DeleteTechZone;

public record DeleteTechZoneCommand(Guid Id) : IRequest<ResponseModel<bool>>;

public class DeleteTechZoneCommandHandler : IRequestHandler<DeleteTechZoneCommand, ResponseModel<bool>>
{
    private readonly ITechZoneRepository _techZoneRepository;
    private readonly IPostRepository _postRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public DeleteTechZoneCommandHandler(
        ITechZoneRepository techZoneRepository,
        IPostRepository postRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser)
    {
        _techZoneRepository = techZoneRepository;
        _postRepository = postRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ResponseModel<bool>> Handle(DeleteTechZoneCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.IsAdmin)
            throw new ForbiddenException("Only Admin can delete TechZones.");

        var zone = await _techZoneRepository.GetByIdAsync(request.Id, cancellationToken);
        if (zone == null)
            throw new NotFoundException(nameof(TechZone), request.Id);

        // Kiểm tra an toàn: nếu còn bài viết thuộc zone này thì không cho xóa
        var hasPosts = await _postRepository.GetByExpression(p => p.TechZoneId == request.Id, cancellationToken).AnyAsync(cancellationToken);
        if (hasPosts || zone.PostsCount > 0)
        {
            throw new Community.Application.Common.Exceptions.ValidationException(new List<FluentValidation.Results.ValidationFailure>
            {
                new("TechZone", "Cannot delete TechZone because it contains posts. Deactivate it instead.")
            });
        }

        await _techZoneRepository.DeleteAsync(zone.Id, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        return ResponseModel<bool>.Success(true, "TechZone deleted successfully.");
    }
}
