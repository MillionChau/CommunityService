using Community.Application.Common.Models;
using Community.Application.DTOs;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Domain.Enums;
using Community.Domain.Interfaces;
using AutoMapper;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Community.Application.Features.PostReport.Commands.CreatePostReport;

// Alias: trong namespace này, "PostReport" bị resolve thành namespace Features.PostReport
using PostReportEntity = Community.Domain.Entities.PostReport;

public record CreatePostReportCommand : IRequest<ResponseModel<PostReportDto>>
{
    public Guid PostId { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public class CreatePostReportCommandValidator : AbstractValidator<CreatePostReportCommand>
{
    public CreatePostReportCommandValidator()
    {
        RuleFor(x => x.PostId).NotEmpty().WithMessage("PostId is required.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required.")
            .MaximumLength(500).WithMessage("Reason must not exceed 500 characters.");
    }
}

/// <summary>
/// Báo cáo bài viết vi phạm (FR-23 / UC-23). ReporterId lấy từ JWT của người dùng hiện tại.
/// Ràng buộc nghiệp vụ: mỗi người dùng chỉ được có MỘT báo cáo đang chờ duyệt cho mỗi bài viết.
/// Báo cáo mới luôn ở trạng thái Pending, chờ Admin xử lý (UC: Hàng chờ duyệt nội dung).
/// </summary>
public class CreatePostReportCommandHandler : IRequestHandler<CreatePostReportCommand, ResponseModel<PostReportDto>>
{
    private readonly IPostReportRepository _postReportRepository;
    private readonly IPostRepository _postRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IMapper _mapper;

    public CreatePostReportCommandHandler(
        IPostReportRepository postReportRepository,
        IPostRepository postRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IMapper mapper)
    {
        _postReportRepository = postReportRepository;
        _postRepository = postRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<ResponseModel<PostReportDto>> Handle(CreatePostReportCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException("Authentication is required.");

        var reporterId = _currentUser.UserId.Value;

        // Bài viết phải tồn tại và chưa bị xóa
        var post = await _postRepository.GetByIdAsync(request.PostId, cancellationToken);
        if (post == null)
            throw new Community.Application.Common.Exceptions.NotFoundException("Post", request.PostId);

        // Chặn báo cáo trùng: cùng user + cùng bài + còn đang chờ duyệt
        var existing = await _postReportRepository
            .GetByExpression(r => r.PostId == request.PostId
                                  && r.ReporterId == reporterId
                                  && r.Status == (int)ReportStatus.Pending)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
            return ResponseModel<PostReportDto>.Error(
                "You have already reported this post. Please wait for the moderators to review it.");

        var postReport = new PostReportEntity
        {
            Id = Guid.NewGuid(),
            PostId = request.PostId,
            ReporterId = reporterId,
            Reason = request.Reason,
            Status = (int)ReportStatus.Pending
        };

        await _postReportRepository.AddAsync(postReport, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        var dto = _mapper.Map<PostReportDto>(postReport);
        return ResponseModel<PostReportDto>.Success(dto, "Report submitted successfully.");
    }
}
