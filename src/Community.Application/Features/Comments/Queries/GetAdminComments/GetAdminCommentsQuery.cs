using AutoMapper;
using Community.Application.Common.Exceptions;
using Community.Application.Common.Models;
using Community.Application.DTOs;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Community.Application.Features.Comments.Queries.GetAdminComments;

public record GetAdminCommentsQuery : IRequest<ResponseModel<PagedResponse<CommentDto>>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? Search { get; init; }
    public int? Status { get; init; }
    public int? IsApproved { get; init; }
    public Guid? PostId { get; init; }
}

public class GetAdminCommentsQueryValidator : AbstractValidator<GetAdminCommentsQuery>
{
    public GetAdminCommentsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0).WithMessage("Page must be greater than 0.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
    }
}

/// <summary>
/// Quản trị viên xem danh sách toàn bộ bình luận trong hệ thống (UC-57)
/// Phân trang, lọc theo bài viết, trạng thái xuất bản, trạng thái AI kiểm duyệt và tìm kiếm từ khóa.
/// </summary>
public class GetAdminCommentsQueryHandler : IRequestHandler<GetAdminCommentsQuery, ResponseModel<PagedResponse<CommentDto>>>
{
    private readonly ICommentRepository _commentRepository;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUser;

    public GetAdminCommentsQueryHandler(
        ICommentRepository commentRepository,
        IMapper mapper,
        ICurrentUserService currentUser)
    {
        _commentRepository = commentRepository;
        _mapper = mapper;
        _currentUser = currentUser;
    }

    public async Task<ResponseModel<PagedResponse<CommentDto>>> Handle(GetAdminCommentsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.IsAdmin)
            throw new ForbiddenException("Only administrators can access admin comments management.");

        var query = _commentRepository.GetByExpression(cancellationToken: cancellationToken);

        // Lọc theo PostId
        if (request.PostId.HasValue)
        {
            query = query.Where(c => c.PostId == request.PostId.Value);
        }

        // Lọc theo Status nếu có
        if (request.Status.HasValue)
        {
            query = query.Where(c => c.Status == request.Status.Value);
        }
        else
        {
            // Mặc định hiển thị trừ bình luận đã xóa
            query = query.Where(c => c.Status != (int)ContentStatus.Deleted);
        }

        // Lọc theo IsApproved (0 = Flagged/Chờ duyệt, 1 = Approved)
        if (request.IsApproved.HasValue)
        {
            query = query.Where(c => c.IsApproved == request.IsApproved.Value);
        }

        // Tìm kiếm từ khóa nội dung
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var keyword = request.Search.Trim().ToLower();
            query = query.Where(c => c.Content != null && c.Content.ToLower().Contains(keyword));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var comments = await query
            .OrderByDescending(c => c.CreatedDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var dtos = _mapper.Map<IEnumerable<CommentDto>>(comments).ToList();
        var pagedResponse = PagedResponse<CommentDto>.Create(dtos, request.Page, request.PageSize, totalCount);
        return ResponseModel<PagedResponse<CommentDto>>.Success(pagedResponse);
    }
}

