using BookKiosk.Application.DTOs.Inventory;
using BookKiosk.Application.Interfaces.Repositories;
using BookKiosk.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BookKiosk.Application.Services;

public interface ISupplierService
{
    Task<IEnumerable<SupplierDto>> GetSuppliersAsync();
    Task<SupplierDto> GetSupplierByIdAsync(int id);
    Task<SupplierDto> CreateSupplierAsync(CreateSupplierDto dto);
    Task<SupplierDto> UpdateSupplierAsync(int id, UpdateSupplierDto dto);
    Task<bool> DeleteSupplierAsync(int id);
}

public class SupplierService : ISupplierService
{
    private readonly ISupplierRepository _supplierRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SupplierService(ISupplierRepository supplierRepository, IUnitOfWork unitOfWork)
    {
        _supplierRepository = supplierRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<SupplierDto>> GetSuppliersAsync()
    {
        var suppliers = await _supplierRepository.GetAllAsync();
        return suppliers.Select(s => new SupplierDto
        {
            SupplierId = s.SupplierId,
            Name = s.Name,
            PhoneNumber = s.PhoneNumber,
            Email = s.Email,
            Address = s.Address,
            IsActive = s.IsActive
        }).ToList();
    }

    public async Task<SupplierDto> GetSupplierByIdAsync(int id)
    {
        var s = await _supplierRepository.GetByIdAsync(id);
        if (s == null)
            throw new KeyNotFoundException("Không tìm thấy thông tin nhà cung cấp.");

        return new SupplierDto
        {
            SupplierId = s.SupplierId,
            Name = s.Name,
            PhoneNumber = s.PhoneNumber,
            Email = s.Email,
            Address = s.Address,
            IsActive = s.IsActive
        };
    }

    public async Task<SupplierDto> CreateSupplierAsync(CreateSupplierDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Tên nhà cung cấp không được để trống.");

        var phone = !string.IsNullOrWhiteSpace(dto.PhoneNumber) ? dto.PhoneNumber : dto.Phone;
        if (string.IsNullOrWhiteSpace(phone))
            throw new ArgumentException("Số điện thoại không được để trống.");

        var supplier = new Supplier
        {
            Name = dto.Name.Trim(),
            PhoneNumber = phone.Trim(),
            Email = dto.Email?.Trim(),
            Address = dto.Address?.Trim(),
            IsActive = dto.IsActive
        };

        await _supplierRepository.AddAsync(supplier);
        await _unitOfWork.SaveChangesAsync();

        return new SupplierDto
        {
            SupplierId = supplier.SupplierId,
            Name = supplier.Name,
            ContactName = dto.ContactName,
            PhoneNumber = supplier.PhoneNumber,
            Email = supplier.Email,
            Address = supplier.Address,
            IsActive = supplier.IsActive
        };
    }

    public async Task<SupplierDto> UpdateSupplierAsync(int id, UpdateSupplierDto dto)
    {
        var supplier = await _supplierRepository.GetByIdAsync(id);
        if (supplier == null)
            throw new KeyNotFoundException("Không tìm thấy thông tin nhà cung cấp.");

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Tên nhà cung cấp không được để trống.");

        var phone = !string.IsNullOrWhiteSpace(dto.PhoneNumber) ? dto.PhoneNumber : dto.Phone;
        if (string.IsNullOrWhiteSpace(phone))
            throw new ArgumentException("Số điện thoại không được để trống.");

        supplier.Name = dto.Name.Trim();
        supplier.PhoneNumber = phone.Trim();
        supplier.Email = dto.Email?.Trim();
        supplier.Address = dto.Address?.Trim();
        supplier.IsActive = dto.IsActive;

        await _supplierRepository.UpdateAsync(supplier);
        await _unitOfWork.SaveChangesAsync();

        return new SupplierDto
        {
            SupplierId = supplier.SupplierId,
            Name = supplier.Name,
            ContactName = dto.ContactName,
            PhoneNumber = supplier.PhoneNumber,
            Email = supplier.Email,
            Address = supplier.Address,
            IsActive = supplier.IsActive
        };
    }

    public async Task<bool> DeleteSupplierAsync(int id)
    {
        var supplier = await _supplierRepository.GetByIdAsync(id);
        if (supplier == null)
            throw new KeyNotFoundException("Không tìm thấy thông tin nhà cung cấp.");

        var hasLinkedReceipts = await _supplierRepository.HasLinkedReceiptsAsync(id);
        if (hasLinkedReceipts)
        {
            // Soft-delete để bảo toàn lịch sử phiếu nhập kho
            supplier.IsActive = false;
            await _supplierRepository.UpdateAsync(supplier);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        await _supplierRepository.DeleteAsync(supplier);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }
}
