using AutoMapper;
using BookKiosk.Application.DTOs.Common;
using BookKiosk.Application.DTOs.Promotions;
using BookKiosk.Domain.Entities;
using BookKiosk.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BookKiosk.Application.Services;

public interface IPromotionService
{
    Task<ApiResponseDto<IEnumerable<PromotionDto>>> GetPromotionsAsync();
    Task<ApiResponseDto<PromotionDto>> CreatePromotionAsync(CreatePromotionDto request);
    Task<ApiResponseDto<PromotionDto>> UpdatePromotionAsync(int id, PromotionDto request);
    Task<ApiResponseDto<bool>> DeletePromotionAsync(int id);
}

public class PromotionService : IPromotionService
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;

    public PromotionService(ApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<ApiResponseDto<IEnumerable<PromotionDto>>> GetPromotionsAsync()
    {
        var promotions = await _context.Promotions
            .Include(p => p.OrderDiscount)
            .OrderByDescending(p => p.PromotionId)
            .ToListAsync();
        return ApiResponseDto<IEnumerable<PromotionDto>>.Ok(_mapper.Map<IEnumerable<PromotionDto>>(promotions));
    }

    public async Task<ApiResponseDto<PromotionDto>> CreatePromotionAsync(CreatePromotionDto request)
    {
        var promotion = _mapper.Map<Promotion>(request);
        _context.Promotions.Add(promotion);
        await _context.SaveChangesAsync();
        return ApiResponseDto<PromotionDto>.Ok(_mapper.Map<PromotionDto>(promotion));
    }

    public async Task<ApiResponseDto<PromotionDto>> UpdatePromotionAsync(int id, PromotionDto request)
    {
        var promotion = await _context.Promotions.FindAsync(id);
        if (promotion == null) return ApiResponseDto<PromotionDto>.Error("NOT_FOUND", "Không tìm thấy khuyến mãi.");

        promotion.Name = request.Name;
        promotion.Description = request.Description;
        promotion.StartDate = request.StartDate;
        promotion.EndDate = request.EndDate;
        promotion.IsActive = request.IsActive;

        await _context.SaveChangesAsync();
        return ApiResponseDto<PromotionDto>.Ok(_mapper.Map<PromotionDto>(promotion));
    }

    public async Task<ApiResponseDto<bool>> DeletePromotionAsync(int id)
    {
        var promotion = await _context.Promotions.FindAsync(id);
        if (promotion == null) return ApiResponseDto<bool>.Error("NOT_FOUND", "Không tìm thấy khuyến mãi.");

        promotion.IsActive = false;
        await _context.SaveChangesAsync();
        return ApiResponseDto<bool>.Ok(true);
    }
}
