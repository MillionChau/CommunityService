using AutoMapper;
using Community.Application.Common.Models;
using Community.Application.DTOs;
using Community.Domain.Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Community.Application.Features.TechZones.Queries.GetActiveTechZones;

/// <summary>
/// UC-14: Lấy danh sách các TechZone đang kích hoạt cho client.
/// </summary>
public record GetActiveTechZonesQuery : IRequest<ResponseModel<IEnumerable<TechZoneDto>>>;

public class GetActiveTechZonesQueryHandler : IRequestHandler<GetActiveTechZonesQuery, ResponseModel<IEnumerable<TechZoneDto>>>
{
    private readonly ITechZoneRepository _techZoneRepository;
    private readonly IMapper _mapper;

    public GetActiveTechZonesQueryHandler(ITechZoneRepository techZoneRepository, IMapper mapper)
    {
        _techZoneRepository = techZoneRepository;
        _mapper = mapper;
    }

    public async Task<ResponseModel<IEnumerable<TechZoneDto>>> Handle(GetActiveTechZonesQuery request, CancellationToken cancellationToken)
    {
        var zones = await _techZoneRepository
            .GetByExpression(t => t.Status == 1, cancellationToken)
            .OrderBy(t => t.DisplayOrder)
            .ThenBy(t => t.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var dtos = _mapper.Map<IEnumerable<TechZoneDto>>(zones);
        return ResponseModel<IEnumerable<TechZoneDto>>.Success(dtos);
    }
}
