using AutoMapper;
using BookKiosk.Application.DTOs.Common;
using BookKiosk.Application.DTOs.Promotions;
using BookKiosk.Application.Interfaces.Repositories;
using BookKiosk.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BookKiosk.Application.Services;

public interface IPromotionService
{
    Task<ApiResponseDto<IEnumerable<PromotionDto>>> GetPromotionsAsync();
    Task<ApiResponseDto<PromotionDto>> CreatePromotionAsync(CreatePromotionDto request);
    Task<ApiResponseDto<PromotionDto>> UpdatePromotionAsync(int id, PromotionDto request);
    Task<ApiResponseDto<bool>> DeletePromotionAsync(int id);
    Task<ApiResponseDto<bool>> TogglePromotionActiveAsync(int id, bool isActive);
}

public class PromotionService : IPromotionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPromotionRepository _promotionRepository;
    private readonly IMapper _mapper;

    public PromotionService(IUnitOfWork unitOfWork, IPromotionRepository promotionRepository, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _promotionRepository = promotionRepository;
        _mapper = mapper;
    }

    public async Task<ApiResponseDto<IEnumerable<PromotionDto>>> GetPromotionsAsync()
    {
        var promotions = await _promotionRepository.GetAllAsync();
        return ApiResponseDto<IEnumerable<PromotionDto>>.Ok(_mapper.Map<IEnumerable<PromotionDto>>(promotions));
    }

    public async Task<ApiResponseDto<PromotionDto>> CreatePromotionAsync(CreatePromotionDto request)
    {
        var promotion = _mapper.Map<Promotion>(request);
        await _promotionRepository.AddAsync(promotion);
        await _unitOfWork.SaveChangesAsync();
        return ApiResponseDto<PromotionDto>.Ok(_mapper.Map<PromotionDto>(promotion));
    }

    public async Task<ApiResponseDto<PromotionDto>> UpdatePromotionAsync(int id, PromotionDto request)
    {
        var promotion = await _promotionRepository.GetByIdAsync(id);
        if (promotion == null) return ApiResponseDto<PromotionDto>.Error("NOT_FOUND", "Không tìm thấy khuyến mãi.");

        promotion.Name = request.Name;
        promotion.Description = request.Description;
        promotion.StartDate = request.StartDate;
        promotion.EndDate = request.EndDate;
        promotion.IsActive = request.IsActive;

        await _unitOfWork.SaveChangesAsync();
        return ApiResponseDto<PromotionDto>.Ok(_mapper.Map<PromotionDto>(promotion));
    }

    public async Task<ApiResponseDto<bool>> TogglePromotionActiveAsync(int id, bool isActive)
    {
        var promotion = await _promotionRepository.GetByIdAsync(id);
        if (promotion == null)
            return ApiResponseDto<bool>.Error("NOT_FOUND", "Không tìm thấy khuyến mãi.");

        if (isActive && promotion.EndDate < System.DateTime.UtcNow)
        {
            return ApiResponseDto<bool>.Error("CONFLICT", "Không thể kích hoạt chương trình khuyến mãi đã hết hạn.");
        }

        promotion.IsActive = isActive;
        await _unitOfWork.SaveChangesAsync();
        return ApiResponseDto<bool>.Ok(true, isActive ? "Kích hoạt chương trình khuyến mãi thành công." : "Tạm ngưng chương trình khuyến mãi thành công.");
    }

    public async Task<ApiResponseDto<bool>> DeletePromotionAsync(int id)
    {
        var promotion = await _promotionRepository.GetByIdAsync(id);
        if (promotion == null) return ApiResponseDto<bool>.Error("NOT_FOUND", "Không tìm thấy khuyến mãi.");

        promotion.IsActive = false;
        await _unitOfWork.SaveChangesAsync();
        return ApiResponseDto<bool>.Ok(true);
    }
}
