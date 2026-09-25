using AutoMapper;
using Community.Application.Common.Models;
using Community.Application.DTOs;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Community.Application.Features.PostReport.Queries.GetPendingReports;

public record GetPendingReportsQuery : IRequest<ResponseModel<PagedResponse<PostReportDto>>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public class GetPendingReportsQueryValidator : AbstractValidator<GetPendingReportsQuery>
{
    public GetPendingReportsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0).WithMessage("Page must be greater than 0.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
    }
}

/// <summary>
/// Xem hàng chờ báo cáo đang chờ duyệt (FR-23 ngữ cảnh Admin / UC-23: hệ thống ghi nhận
/// báo cáo để xử lý; màn hình Admin "Hàng chờ duyệt nội dung"). Chỉ Admin được truy cập.
/// </summary>
public class GetPendingReportsQueryHandler
    : IRequestHandler<GetPendingReportsQuery, ResponseModel<PagedResponse<PostReportDto>>>
{
    private readonly IPostReportRepository _postReportRepository;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUser;

    public GetPendingReportsQueryHandler(IPostReportRepository postReportRepository, IMapper mapper,
        ICurrentUserService currentUser)
    {
        _postReportRepository = postReportRepository;
        _mapper = mapper;
        _currentUser = currentUser;
    }

    public async Task<ResponseModel<PagedResponse<PostReportDto>>> Handle(GetPendingReportsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            throw new UnauthorizedAccessException("Only administrators can view pending reports.");

        var query = _postReportRepository
            .GetByExpression(r => r.Status == (int)ReportStatus.Pending, cancellationToken)
            .OrderBy(r => r.CreatedDate); // cũ nhất trước — báo cáo lâu chưa xử lý lên đầu

        var totalCount = await query.CountAsync(cancellationToken);

        var reports = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var dtos = _mapper.Map<IEnumerable<PostReportDto>>(reports).ToList();
        return ResponseModel<PagedResponse<PostReportDto>>.Success(
            PagedResponse<PostReportDto>.Create(dtos, request.Page, request.PageSize, totalCount));
    }
}
