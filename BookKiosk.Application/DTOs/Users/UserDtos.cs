using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using BookKiosk.Domain.Enums;

namespace BookKiosk.Application.DTOs.Users;

public class UserDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UserRole Role { get; set; }

    public bool IsActive { get; set; }
}

public class UserFilterDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Keyword { get; set; }
    public UserRole? Role { get; set; }
    public bool? IsActive { get; set; }
}

public class CreateUserDto
{
    [Required(ErrorMessage = "Tên đăng nhập không được để trống")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Tên đăng nhập từ 3 đến 50 ký tự")]
    [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Tên đăng nhập chỉ chứa chữ cái, số và dấu gạch dưới")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu không được để trống")]
    [MinLength(8, ErrorMessage = "Mật khẩu phải có tối thiểu 8 ký tự")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Họ và tên không được để trống")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Họ và tên từ 2 đến 100 ký tự")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vai trò không được để trống")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UserRole Role { get; set; }
}

public class UpdateUserDto
{
    [Required(ErrorMessage = "Họ và tên không được để trống")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Họ và tên từ 2 đến 100 ký tự")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vai trò không được để trống")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UserRole Role { get; set; }
}

public class UpdateUserStatusDto
{
    public bool IsActive { get; set; }
}

public class ResetPasswordDto
{
    [Required(ErrorMessage = "Mật khẩu mới không được để trống")]
    [MinLength(8, ErrorMessage = "Mật khẩu mới phải có tối thiểu 8 ký tự")]
    public string NewPassword { get; set; } = string.Empty;
}
