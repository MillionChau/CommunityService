using AutoMapper;
using Community.Application.Common.Exceptions;
using Community.Application.Common.Models;
using Community.Application.DTOs;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using MediatR;

namespace Community.Application.Features.TechZones.Queries.GetTechZoneBySlug;

/// <summary>
/// UC-14: Lấy chi tiết TechZone theo Slug.
/// </summary>
public record GetTechZoneBySlugQuery(string Slug) : IRequest<ResponseModel<TechZoneDto>>;

public class GetTechZoneBySlugQueryHandler : IRequestHandler<GetTechZoneBySlugQuery, ResponseModel<TechZoneDto>>
{
    private readonly ITechZoneRepository _techZoneRepository;
    private readonly IMapper _mapper;

    public GetTechZoneBySlugQueryHandler(ITechZoneRepository techZoneRepository, IMapper mapper)
    {
        _techZoneRepository = techZoneRepository;
        _mapper = mapper;
    }

    public async Task<ResponseModel<TechZoneDto>> Handle(GetTechZoneBySlugQuery request, CancellationToken cancellationToken)
    {
        var zone = await _techZoneRepository.GetBySlugAsync(request.Slug, cancellationToken);
        if (zone == null)
            throw new NotFoundException(nameof(TechZone), request.Slug);

        var dto = _mapper.Map<TechZoneDto>(zone);
        return ResponseModel<TechZoneDto>.Success(dto);
    }
}
