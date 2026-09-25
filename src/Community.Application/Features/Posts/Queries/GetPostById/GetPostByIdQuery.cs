using AutoMapper;
using Community.Application.Common.Exceptions;
using Community.Application.Common.Models;
using Community.Application.DTOs;
using Community.Application.Interfaces;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Community.Application.Features.Posts.Queries.GetPostById;

public record GetPostByIdQuery(Guid Id) : IRequest<ResponseModel<PostDto>>;

public class GetPostByIdQueryHandler : IRequestHandler<GetPostByIdQuery, ResponseModel<PostDto>>
{
    private readonly IPostRepository _postRepository;
    private readonly IPostLikeRepository _likeRepository;
    private readonly IPostBookmarkRepository _bookmarkRepository;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUser;

    public GetPostByIdQueryHandler(
        IPostRepository postRepository,
        IPostLikeRepository likeRepository,
        IPostBookmarkRepository bookmarkRepository,
        IMapper mapper,
        ICurrentUserService currentUser)
    {
        _postRepository = postRepository;
        _likeRepository = likeRepository;
        _bookmarkRepository = bookmarkRepository;
        _mapper = mapper;
        _currentUser = currentUser;
    }

    public async Task<ResponseModel<PostDto>> Handle(GetPostByIdQuery request, CancellationToken cancellationToken)
    {
        var post = await _postRepository.GetByIdAsync(request.Id, cancellationToken);
        if (post == null)
            throw new NotFoundException(nameof(Post), request.Id);

        var dto = _mapper.Map<PostDto>(post);

        // Trạng thái tương tác của người xem hiện tại (IsLiked/IsBookmarked) theo JWT
        if (_currentUser.UserId is Guid viewerId)
        {
            dto.IsLikedByViewer = await _likeRepository
                .GetByExpression(l => l.UserId == viewerId && l.PostId == post.Id, cancellationToken)
                .AnyAsync(cancellationToken);
            dto.IsBookmarkedByViewer = await _bookmarkRepository
                .GetByExpression(b => b.UserId == viewerId && b.PostId == post.Id, cancellationToken)
                .AnyAsync(cancellationToken);
        }

        return ResponseModel<PostDto>.Success(dto);
    }
}
