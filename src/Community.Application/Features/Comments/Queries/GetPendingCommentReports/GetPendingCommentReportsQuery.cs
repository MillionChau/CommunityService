using AutoMapper;
using Community.Application.Common.Models;
using Community.Application.DTOs;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Community.Application.Features.Comments.Queries.GetPendingCommentReports;

public record GetPendingCommentReportsQuery : IRequest<ResponseModel<PagedResponse<CommentReportDto>>>
{
    public int PageIndex { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}

/// <summary>
/// Admin lấy danh sách báo cáo bình luận đang chờ duyệt (Pending) (UC-29 / UC-62).
/// </summary>
public class GetPendingCommentReportsQueryHandler
    : IRequestHandler<GetPendingCommentReportsQuery, ResponseModel<PagedResponse<CommentReportDto>>>
{
    private readonly ICommentReportRepository _commentReportRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IMapper _mapper;

    public GetPendingCommentReportsQueryHandler(
        ICommentReportRepository commentReportRepository,
        ICurrentUserService currentUser,
        IMapper mapper)
    {
        _commentReportRepository = commentReportRepository;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<ResponseModel<PagedResponse<CommentReportDto>>> Handle(
        GetPendingCommentReportsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            throw new UnauthorizedAccessException("Only administrators can view pending reports.");

        var query = _commentReportRepository
            .GetByExpression(r => r.Status == (int)ReportStatus.Pending)
            .OrderByDescending(r => r.CreatedDate);

        var totalCount = await query.CountAsync(cancellationToken);
        var pageIndex = Math.Max(request.PageIndex, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var items = await query
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = _mapper.Map<IEnumerable<CommentReportDto>>(items).ToList();
        var paged = PagedResponse<CommentReportDto>.Create(dtos, pageIndex, pageSize, totalCount);

        return ResponseModel<PagedResponse<CommentReportDto>>.Success(paged);
    }
}
