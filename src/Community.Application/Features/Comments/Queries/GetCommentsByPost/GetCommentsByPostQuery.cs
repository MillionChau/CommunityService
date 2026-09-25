using AutoMapper;
using Community.Application.Common.Exceptions;
using Community.Application.Common.Models;
using Community.Application.DTOs;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Community.Application.Features.Comments.Queries.GetCommentsByPost;

public record GetCommentsByPostQuery(Guid PostId) : IRequest<ResponseModel<IEnumerable<CommentDto>>>;

/// <summary>
/// Xem các bình luận của một bài viết (FR-24 ngữ cảnh đọc).
/// Chỉ trả bình luận Published; Admin xem được tất cả trừ Deleted.
/// Sắp xếp: bình luận gốc trước theo mới nhất, reply đi kèm ParentCommentId để client dựng cây.
/// </summary>
public class GetCommentsByPostQueryHandler
    : IRequestHandler<GetCommentsByPostQuery, ResponseModel<IEnumerable<CommentDto>>>
{
    private readonly ICommentRepository _commentRepository;
    private readonly IPostRepository _postRepository;
    private readonly ICommentLikeRepository _likeRepository;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUser;

    public GetCommentsByPostQueryHandler(
        ICommentRepository commentRepository,
        IPostRepository postRepository,
        ICommentLikeRepository likeRepository,
        IMapper mapper,
        ICurrentUserService currentUser)
    {
        _commentRepository = commentRepository;
        _postRepository = postRepository;
        _likeRepository = likeRepository;
        _mapper = mapper;
        _currentUser = currentUser;
    }

    public async Task<ResponseModel<IEnumerable<CommentDto>>> Handle(GetCommentsByPostQuery request,
        CancellationToken cancellationToken)
    {
        // Bài viết phải tồn tại và không bị xóa
        var post = await _postRepository.GetByIdAsync(request.PostId, cancellationToken);
        if (post == null || post.Status == (int)ContentStatus.Deleted)
            throw new NotFoundException(nameof(Post), request.PostId);

        var comments = await _commentRepository.GetCommentsByPostIdAsync(request.PostId, cancellationToken);

        if (!_currentUser.IsAdmin)
        {
            comments = comments.Where(c => c.Status == null || c.Status == (int)ContentStatus.Published);
        }
        else
        {
            comments = comments.Where(c => c.Status != (int)ContentStatus.Deleted);
        }

        var dtos = _mapper.Map<IEnumerable<CommentDto>>(comments).ToList();

        // Người xem hiện tại đã thích những bình luận nào (IsLikedByViewer) theo JWT
        if (_currentUser.UserId is Guid viewerId && dtos.Count > 0)
        {
            var commentIds = dtos.Select(c => c.Id).ToList();
            var likedIds = (await _likeRepository
                    .GetByExpression(l => l.UserId == viewerId && commentIds.Contains(l.CommentId), cancellationToken)
                    .Select(l => l.CommentId)
                    .ToListAsync(cancellationToken))
                .ToHashSet();

            foreach (var dto in dtos)
                dto.IsLikedByViewer = likedIds.Contains(dto.Id);
        }

        return ResponseModel<IEnumerable<CommentDto>>.Success(dtos);
    }
}
