using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BookKiosk.Application.DTOs.Inventory;

public class SupplierDto
{
    public int SupplierId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? PhoneNumber { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone
    {
        get => PhoneNumber;
        set => PhoneNumber = value;
    }

    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CreateSupplierDto
{
    [Required(ErrorMessage = "Tên nhà cung cấp không được để trống")]
    [MaxLength(255, ErrorMessage = "Tên nhà cung cấp tối đa 255 ký tự")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100, ErrorMessage = "Tên người liên hệ tối đa 100 ký tự")]
    public string? ContactName { get; set; }

    [Required(ErrorMessage = "Số điện thoại không được để trống")]
    [MaxLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự")]
    public string PhoneNumber { get; set; } = string.Empty;

    [JsonPropertyName("phone")]
    public string? Phone
    {
        get => PhoneNumber;
        set => PhoneNumber = value ?? string.Empty;
    }

    [MaxLength(100, ErrorMessage = "Email tối đa 100 ký tự")]
    public string? Email { get; set; }

    [MaxLength(500, ErrorMessage = "Địa chỉ tối đa 500 ký tự")]
    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateSupplierDto
{
    [Required(ErrorMessage = "Tên nhà cung cấp không được để trống")]
    [MaxLength(255, ErrorMessage = "Tên nhà cung cấp tối đa 255 ký tự")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100, ErrorMessage = "Tên người liên hệ tối đa 100 ký tự")]
    public string? ContactName { get; set; }

    [Required(ErrorMessage = "Số điện thoại không được để trống")]
    [MaxLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự")]
    public string PhoneNumber { get; set; } = string.Empty;

    [JsonPropertyName("phone")]
    public string? Phone
    {
        get => PhoneNumber;
        set => PhoneNumber = value ?? string.Empty;
    }

    [MaxLength(100, ErrorMessage = "Email tối đa 100 ký tự")]
    public string? Email { get; set; }

    [MaxLength(500, ErrorMessage = "Địa chỉ tối đa 500 ký tự")]
    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;
}
