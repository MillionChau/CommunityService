using AutoMapper;
using Community.Application.Common.Exceptions;
using Community.Application.Common.Models;
using Community.Application.DTOs;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using FluentValidation;
using MediatR;

namespace Community.Application.Features.Comments.Queries.GetAdminCommentById;

public record GetAdminCommentByIdQuery(Guid Id) : IRequest<ResponseModel<CommentDto>>;

public class GetAdminCommentByIdQueryValidator : AbstractValidator<GetAdminCommentByIdQuery>
{
    public GetAdminCommentByIdQueryValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("CommentId is required.");
    }
}

/// <summary>
/// Quản trị viên xem thông tin chi tiết một bình luận (UC-58).
/// </summary>
public class GetAdminCommentByIdQueryHandler : IRequestHandler<GetAdminCommentByIdQuery, ResponseModel<CommentDto>>
{
    private readonly ICommentRepository _commentRepository;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUser;

    public GetAdminCommentByIdQueryHandler(
        ICommentRepository commentRepository,
        IMapper mapper,
        ICurrentUserService currentUser)
    {
        _commentRepository = commentRepository;
        _mapper = mapper;
        _currentUser = currentUser;
    }

    public async Task<ResponseModel<CommentDto>> Handle(GetAdminCommentByIdQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.IsAdmin)
            throw new ForbiddenException("Only administrators can view comment details via admin endpoint.");

        var comment = await _commentRepository.GetByIdAsync(request.Id, cancellationToken);
        if (comment == null)
            throw new NotFoundException(nameof(Comment), request.Id);

        var dto = _mapper.Map<CommentDto>(comment);
        return ResponseModel<CommentDto>.Success(dto);
    }
}
