using AutoMapper;
using Community.Application.Common.Models;
using Community.Application.DTOs;
using Community.Domain.Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Community.Application.Features.Follows.Queries.GetFollowers;

public record GetFollowersQuery(Guid UserId, int PageIndex = 1, int PageSize = 20)
    : IRequest<ResponseModel<PagedResponse<UserFollowDto>>>;

public class GetFollowersQueryHandler
    : IRequestHandler<GetFollowersQuery, ResponseModel<PagedResponse<UserFollowDto>>>
{
    private readonly IUserFollowRepository _userFollowRepository;
    private readonly IMapper _mapper;

    public GetFollowersQueryHandler(IUserFollowRepository userFollowRepository, IMapper mapper)
    {
        _userFollowRepository = userFollowRepository;
        _mapper = mapper;
    }

    public async Task<ResponseModel<PagedResponse<UserFollowDto>>> Handle(
        GetFollowersQuery request, CancellationToken cancellationToken)
    {
        var query = _userFollowRepository
            .GetByExpression(f => f.FolloweeId == request.UserId)
            .OrderByDescending(f => f.CreatedDate);

        var totalCount = await query.CountAsync(cancellationToken);
        var pageIndex = Math.Max(request.PageIndex, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var items = await query
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = _mapper.Map<IEnumerable<UserFollowDto>>(items).ToList();
        var paged = PagedResponse<UserFollowDto>.Create(dtos, pageIndex, pageSize, totalCount);

        return ResponseModel<PagedResponse<UserFollowDto>>.Success(paged);
    }
}
