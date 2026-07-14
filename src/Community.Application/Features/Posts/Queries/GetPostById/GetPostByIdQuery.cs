using AutoMapper;
using Community.Application.Common.Exceptions;
using Community.Application.Common.Models;
using Community.Application.DTOs;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using MediatR;

namespace Community.Application.Features.Posts.Queries.GetPostById;

public record GetPostByIdQuery(Guid Id) : IRequest<ResponseModel<PostDto>>;

public class GetPostByIdQueryHandler : IRequestHandler<GetPostByIdQuery, ResponseModel<PostDto>>
{
    private readonly IPostRepository _postRepository;
    private readonly IMapper _mapper;

    public GetPostByIdQueryHandler(IPostRepository postRepository, IMapper mapper)
    {
        _postRepository = postRepository;
        _mapper = mapper;
    }

    public async Task<ResponseModel<PostDto>> Handle(GetPostByIdQuery request, CancellationToken cancellationToken)
    {
        var post = await _postRepository.GetByIdAsync(request.Id, cancellationToken);
        if (post == null)
            throw new NotFoundException(nameof(Post), request.Id);

        var dto = _mapper.Map<PostDto>(post);
        return ResponseModel<PostDto>.Success(dto);
    }
}
