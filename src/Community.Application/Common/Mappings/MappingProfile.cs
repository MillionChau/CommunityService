using AutoMapper;
using Community.Domain.Entities;
using Community.Application.DTOs;

namespace Community.Application.Common.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Post, PostDto>();
        CreateMap<Comment, CommentDto>();
        CreateMap<PostReport, PostReportDto>();
    }
}
