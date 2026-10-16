using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BookKiosk.CMS.Models.Inventory
{
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
        [Display(Name = "Tên nhà cung cấp")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100, ErrorMessage = "Tên người liên hệ tối đa 100 ký tự")]
        [Display(Name = "Người đại diện liên hệ")]
        public string? ContactName { get; set; }

        [Required(ErrorMessage = "Số điện thoại không được để trống")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        [MaxLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự")]
        [Display(Name = "Số điện thoại")]
        public string PhoneNumber { get; set; } = string.Empty;

        [JsonPropertyName("phone")]
        public string Phone
        {
            get => PhoneNumber;
            set => PhoneNumber = value;
        }

        [EmailAddress(ErrorMessage = "Địa chỉ email không hợp lệ")]
        [MaxLength(100, ErrorMessage = "Email tối đa 100 ký tự")]
        [Display(Name = "Hộp thư Email")]
        public string? Email { get; set; }

        [MaxLength(500, ErrorMessage = "Địa chỉ tối đa 500 ký tự")]
        [Display(Name = "Địa chỉ văn phòng / kho")]
        public string? Address { get; set; }

        [Display(Name = "Đang giao dịch / Hoạt động")]
        public bool IsActive { get; set; } = true;
    }

    public class UpdateSupplierDto
    {
        [Required(ErrorMessage = "Tên nhà cung cấp không được để trống")]
        [MaxLength(255, ErrorMessage = "Tên nhà cung cấp tối đa 255 ký tự")]
        [Display(Name = "Tên nhà cung cấp")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100, ErrorMessage = "Tên người liên hệ tối đa 100 ký tự")]
        [Display(Name = "Người đại diện liên hệ")]
        public string? ContactName { get; set; }

        [Required(ErrorMessage = "Số điện thoại không được để trống")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        [MaxLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự")]
        [Display(Name = "Số điện thoại")]
        public string PhoneNumber { get; set; } = string.Empty;

        [JsonPropertyName("phone")]
        public string Phone
        {
            get => PhoneNumber;
            set => PhoneNumber = value;
        }

        [EmailAddress(ErrorMessage = "Địa chỉ email không hợp lệ")]
        [MaxLength(100, ErrorMessage = "Email tối đa 100 ký tự")]
        [Display(Name = "Hộp thư Email")]
        public string? Email { get; set; }

        [MaxLength(500, ErrorMessage = "Địa chỉ tối đa 500 ký tự")]
        [Display(Name = "Địa chỉ văn phòng / kho")]
        public string? Address { get; set; }

        [Display(Name = "Đang giao dịch / Hoạt động")]
        public bool IsActive { get; set; } = true;
    }
}
