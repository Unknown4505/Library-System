using AutoMapper;
using BookKiosk.Application.DTOs.Common;
using BookKiosk.Application.DTOs.Promotions;
using BookKiosk.Application.Interfaces.Repositories;
using BookKiosk.Domain.Entities;
using BookKiosk.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BookKiosk.Application.Services;

public interface IPromotionService
{
    Task<ApiResponseDto<IEnumerable<PromotionDto>>> GetPromotionsAsync();
    Task<ApiResponseDto<PromotionDto>> GetPromotionByIdAsync(int id);
    Task<ApiResponseDto<PromotionDto>> CreatePromotionAsync(CreatePromotionDto request);
    Task<ApiResponseDto<PromotionDto>> UpdatePromotionAsync(int id, UpdatePromotionDto request);
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
        var dtos = _mapper.Map<IEnumerable<PromotionDto>>(promotions).ToList();
        foreach (var dto in dtos)
        {
            dto.StartDate = DateTime.SpecifyKind(dto.StartDate, DateTimeKind.Utc);
            dto.EndDate = DateTime.SpecifyKind(dto.EndDate, DateTimeKind.Utc);
        }
        return ApiResponseDto<IEnumerable<PromotionDto>>.Ok(dtos);
    }

    public async Task<ApiResponseDto<PromotionDto>> GetPromotionByIdAsync(int id)
    {
        var promotion = await _promotionRepository.GetByIdAsync(id);
        if (promotion == null)
            return ApiResponseDto<PromotionDto>.Error("NOT_FOUND", "Không tìm thấy khuyến mãi.");

        var dto = _mapper.Map<PromotionDto>(promotion);
        dto.StartDate = DateTime.SpecifyKind(dto.StartDate, DateTimeKind.Utc);
        dto.EndDate = DateTime.SpecifyKind(dto.EndDate, DateTimeKind.Utc);
        return ApiResponseDto<PromotionDto>.Ok(dto);
    }

    public async Task<ApiResponseDto<PromotionDto>> CreatePromotionAsync(CreatePromotionDto request)
    {
        var error = await ValidatePromotionDataAsync(
            request.Name,
            request.StartDate,
            request.EndDate,
            request.PromotionType,
            request.OrderDiscount,
            request.ProductDiscounts);

        if (error != null)
            return ApiResponseDto<PromotionDto>.Error("BAD_REQUEST", error);

        var promotion = new Promotion
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            PromotionType = request.PromotionType,
            StartDate = DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc),
            EndDate = DateTime.SpecifyKind(request.EndDate, DateTimeKind.Utc),
            IsActive = request.IsActive
        };

        if (request.PromotionType == PromotionType.OrderDiscount)
        {
            promotion.OrderDiscount = new PromotionOrderDiscount
            {
                MinOrderValue = request.OrderDiscount!.MinOrderValue,
                DiscountAmount = request.OrderDiscount.DiscountAmount
            };
        }
        else
        {
            foreach (var item in request.ProductDiscounts)
            {
                promotion.ProductDiscounts.Add(new PromotionProductDiscount
                {
                    BookId = item.BookId,
                    DiscountAmount = item.DiscountAmount
                });
            }
        }

        await _promotionRepository.AddAsync(promotion);
        await _unitOfWork.SaveChangesAsync();

        var dto = _mapper.Map<PromotionDto>(promotion);
        dto.StartDate = DateTime.SpecifyKind(promotion.StartDate, DateTimeKind.Utc);
        dto.EndDate = DateTime.SpecifyKind(promotion.EndDate, DateTimeKind.Utc);
        return ApiResponseDto<PromotionDto>.Ok(dto, "Tạo chương trình khuyến mãi thành công.");
    }

    public async Task<ApiResponseDto<PromotionDto>> UpdatePromotionAsync(int id, UpdatePromotionDto request)
    {
        var promotion = await _promotionRepository.GetByIdAsync(id);
        if (promotion == null)
            return ApiResponseDto<PromotionDto>.Error("NOT_FOUND", "Không tìm thấy khuyến mãi.");

        var error = await ValidatePromotionDataAsync(
            request.Name,
            request.StartDate,
            request.EndDate,
            request.PromotionType,
            request.OrderDiscount,
            request.ProductDiscounts);

        if (error != null)
            return ApiResponseDto<PromotionDto>.Error("BAD_REQUEST", error);

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            // Điểm 4: Xóa tường minh qua IPromotionRepository / PromotionRepository (Clean Architecture 100%)
            await _promotionRepository.ClearPromotionDetailsAsync(id);

            promotion.ProductDiscounts.Clear();
            promotion.OrderDiscount = null;

            promotion.Name = request.Name.Trim();
            promotion.Description = request.Description?.Trim();
            promotion.PromotionType = request.PromotionType;
            promotion.StartDate = DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc);
            promotion.EndDate = DateTime.SpecifyKind(request.EndDate, DateTimeKind.Utc);
            promotion.IsActive = request.IsActive;

            if (request.PromotionType == PromotionType.OrderDiscount)
            {
                promotion.OrderDiscount = new PromotionOrderDiscount
                {
                    PromotionId = id,
                    MinOrderValue = request.OrderDiscount!.MinOrderValue,
                    DiscountAmount = request.OrderDiscount.DiscountAmount
                };
            }
            else
            {
                foreach (var item in request.ProductDiscounts)
                {
                    promotion.ProductDiscounts.Add(new PromotionProductDiscount
                    {
                        PromotionId = id,
                        BookId = item.BookId,
                        DiscountAmount = item.DiscountAmount
                    });
                }
            }

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();

            var updated = await _promotionRepository.GetByIdAsync(id) ?? promotion;
            var dto = _mapper.Map<PromotionDto>(updated);
            dto.StartDate = DateTime.SpecifyKind(updated.StartDate, DateTimeKind.Utc);
            dto.EndDate = DateTime.SpecifyKind(updated.EndDate, DateTimeKind.Utc);
            return ApiResponseDto<PromotionDto>.Ok(dto, "Cập nhật chương trình khuyến mãi thành công.");
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    public async Task<ApiResponseDto<PromotionDto>> UpdatePromotionAsync(int id, PromotionDto request)
    {
        var updateDto = new UpdatePromotionDto
        {
            Name = request.Name,
            Description = request.Description,
            PromotionType = request.PromotionType,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = request.IsActive,
            OrderDiscount = request.OrderDiscount,
            ProductDiscounts = request.ProductDiscounts
        };
        return await UpdatePromotionAsync(id, updateDto);
    }

    public async Task<ApiResponseDto<bool>> TogglePromotionActiveAsync(int id, bool isActive)
    {
        var promotion = await _promotionRepository.GetByIdAsync(id);
        if (promotion == null)
            return ApiResponseDto<bool>.Error("NOT_FOUND", "Không tìm thấy khuyến mãi.");

        if (isActive && promotion.EndDate < DateTime.UtcNow)
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

    private async Task<string?> ValidatePromotionDataAsync(
        string name,
        DateTime startDate,
        DateTime endDate,
        PromotionType promotionType,
        PromotionOrderDiscountDto? orderDiscount,
        List<PromotionProductDiscountDto>? productDiscounts)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Tên chương trình khuyến mãi không được để trống.";

        if (startDate > endDate)
            return "Ngày bắt đầu không được lớn hơn ngày kết thúc.";

        if (promotionType == PromotionType.OrderDiscount)
        {
            if (orderDiscount == null)
                return "Chi tiết giảm giá theo đơn hàng không được để trống.";

            if (productDiscounts != null && productDiscounts.Count > 0)
                return "Không được cấu hình chi tiết giảm giá theo sản phẩm cho khuyến mãi loại đơn hàng.";

            if (orderDiscount.MinOrderValue < 0)
                return "Giá trị đơn hàng tối thiểu không hợp lệ.";

            if (orderDiscount.DiscountAmount <= 0)
                return "Số tiền giảm giá phải lớn hơn 0.";

            if (decimal.Truncate(orderDiscount.MinOrderValue) != orderDiscount.MinOrderValue ||
                decimal.Truncate(orderDiscount.DiscountAmount) != orderDiscount.DiscountAmount)
                return "Số tiền phải là số nguyên (VNĐ).";
        }
        else if (promotionType == PromotionType.ProductDiscount)
        {
            if (orderDiscount != null)
                return "Không được cấu hình chi tiết giảm giá theo đơn hàng cho khuyến mãi loại sản phẩm.";

            if (productDiscounts == null || productDiscounts.Count == 0)
                return "Danh sách sản phẩm giảm giá không được để trống.";

            foreach (var item in productDiscounts)
            {
                if (item.DiscountAmount <= 0)
                    return "Số tiền giảm giá sản phẩm phải lớn hơn 0.";

                if (decimal.Truncate(item.DiscountAmount) != item.DiscountAmount)
                    return "Số tiền giảm giá phải là số nguyên (VNĐ).";
            }

            var duplicateBook = productDiscounts.GroupBy(x => x.BookId).FirstOrDefault(g => g.Count() > 1);
            if (duplicateBook != null)
                return $"Trùng lặp cấu hình giảm giá cho sách ID {duplicateBook.Key}.";

            var bookIds = productDiscounts.Select(x => x.BookId).Distinct().ToList();
            var existingBookIds = await _promotionRepository.GetExistingBookIdsAsync(bookIds);
            var missingBookIds = bookIds.Except(existingBookIds).ToList();
            if (missingBookIds.Count > 0)
                return $"Sách với ID {string.Join(", ", missingBookIds)} không tồn tại trong hệ thống.";
        }
        else
        {
            return "Loại khuyến mãi không hợp lệ.";
        }

        return null;
    }
}
