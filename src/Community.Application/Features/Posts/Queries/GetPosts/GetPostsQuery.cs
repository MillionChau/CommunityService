using AutoMapper;
using Community.Application.Common.Models;
using Community.Application.DTOs;
using Community.Domain.Contracts;
using MediatR;

namespace Community.Application.Features.Posts.Queries.GetPosts;

public record GetPostsQuery : IRequest<ResponseModel<IEnumerable<PostDto>>>;

public class GetPostsQueryHandler : IRequestHandler<GetPostsQuery, ResponseModel<IEnumerable<PostDto>>>
{
    private readonly IPostRepository _postRepository;
    private readonly IMapper _mapper;

    public GetPostsQueryHandler(IPostRepository postRepository, IMapper mapper)
    {
        _postRepository = postRepository;
        _mapper = mapper;
    }

    public async Task<ResponseModel<IEnumerable<PostDto>>> Handle(GetPostsQuery request,
        CancellationToken cancellationToken)
    {
        var posts = await _postRepository.GetAllAsync(cancellationToken);
        var dtos = _mapper.Map<IEnumerable<PostDto>>(posts);

        return ResponseModel<IEnumerable<PostDto>>.Success(dtos);
    }
}
