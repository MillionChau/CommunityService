using AutoMapper;
using Community.Domain.Entities;
using Community.Application.DTOs;

namespace Community.Application.Common.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Post, PostDto>()
            .ForMember(d => d.TechZoneName, opt => opt.MapFrom(s => s.TechZone != null ? s.TechZone.Name : null))
            .ForMember(d => d.TechZoneSlug, opt => opt.MapFrom(s => s.TechZone != null ? s.TechZone.Slug : null));
        CreateMap<Comment, CommentDto>();
        CreateMap<PostReport, PostReportDto>();
        CreateMap<CommentReport, CommentReportDto>();
        CreateMap<UserFollow, UserFollowDto>();
        CreateMap<TechZone, TechZoneDto>();
        CreateMap<BookmarkFolder, BookmarkFolderDto>();
    }
}
