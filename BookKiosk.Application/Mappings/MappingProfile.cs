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

        CreateMap<PromotionOrderDiscount, PromotionOrderDiscountDto>().ReverseMap();
        CreateMap<PromotionProductDiscount, PromotionProductDiscountDto>().ReverseMap();
        CreateMap<Promotion, PromotionDto>().ReverseMap();
        CreateMap<CreatePromotionDto, Promotion>()
            .ForMember(destination => destination.PromotionId, options => options.Ignore())
            .ForMember(destination => destination.CreatedAt, options => options.Ignore())
            .ForMember(destination => destination.UpdatedAt, options => options.Ignore());
        CreateMap<UpdatePromotionDto, Promotion>()
            .ForMember(destination => destination.PromotionId, options => options.Ignore())
            .ForMember(destination => destination.CreatedAt, options => options.Ignore())
            .ForMember(destination => destination.UpdatedAt, options => options.Ignore());
    }
}
