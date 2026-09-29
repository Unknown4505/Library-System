using AutoMapper;
using BookKiosk.Domain.Entities;
using BookKiosk.Application.DTOs.Members;
using BookKiosk.Application.DTOs.Promotions;

namespace BookKiosk.Application.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Member, MemberDto>().ReverseMap();
        CreateMap<CreateMemberDto, Member>();
        CreateMap<UpdateMemberDto, Member>();

        CreateMap<Promotion, PromotionDto>().ReverseMap();
        CreateMap<CreatePromotionDto, Promotion>();
    }
}
