using AutoMapper;
using Community.Application.Common.Exceptions;
using Community.Application.Common.Models;
using Community.Application.DTOs;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Community.Application.Features.TechZones.Queries.GetAdminTechZones;

public record GetAdminTechZonesQuery : IRequest<ResponseModel<PagedResponse<TechZoneDto>>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? Search { get; init; }
    public int? Status { get; init; }
}

public class GetAdminTechZonesQueryValidator : AbstractValidator<GetAdminTechZonesQuery>
{
    public GetAdminTechZonesQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0).WithMessage("Page must be greater than 0.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50).WithMessage("PageSize must be between 1 and 50.");
    }
}

public class GetAdminTechZonesQueryHandler : IRequestHandler<GetAdminTechZonesQuery, ResponseModel<PagedResponse<TechZoneDto>>>
{
    private readonly ITechZoneRepository _techZoneRepository;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUser;

    public GetAdminTechZonesQueryHandler(
        ITechZoneRepository techZoneRepository,
        IMapper mapper,
        ICurrentUserService currentUser)
    {
        _techZoneRepository = techZoneRepository;
        _mapper = mapper;
        _currentUser = currentUser;
    }

    public async Task<ResponseModel<PagedResponse<TechZoneDto>>> Handle(GetAdminTechZonesQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.IsAdmin)
            throw new ForbiddenException("Only Admin can access admin tech zones list.");

        var query = _techZoneRepository.GetByExpression(cancellationToken: cancellationToken);

        if (request.Status.HasValue)
        {
            query = query.Where(t => t.Status == request.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var keyword = request.Search.Trim().ToLower();
            query = query.Where(t => t.Name.ToLower().Contains(keyword) || t.Slug.ToLower().Contains(keyword));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var zones = await query
            .OrderBy(t => t.DisplayOrder)
            .ThenByDescending(t => t.CreatedDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var dtos = _mapper.Map<IEnumerable<TechZoneDto>>(zones).ToList();
        var paged = PagedResponse<TechZoneDto>.Create(dtos, request.Page, request.PageSize, totalCount);

        return ResponseModel<PagedResponse<TechZoneDto>>.Success(paged);
    }
}
