using Community.Application.Common.Models;
using Community.Application.DTOs;
using Community.Application.Features.Posts.Commands.CreatePost;
using Community.Application.Features.Posts.Queries.GetPostById;
using Community.Application.Features.Posts.Queries.GetPosts;
using Microsoft.AspNetCore.Mvc;

namespace Community.API.Controllers;

public class PostsController : ApiControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ResponseModel<Guid>>> Create(CreatePostCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Data }, result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ResponseModel<PostDto>>> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetPostByIdQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<ResponseModel<IEnumerable<PostDto>>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetPostsQuery(), cancellationToken);
        return Ok(result);
    }
}
