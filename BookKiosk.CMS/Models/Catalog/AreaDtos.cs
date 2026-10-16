using System.ComponentModel.DataAnnotations;

namespace BookKiosk.CMS.Models.Catalog
{
    public class AreaDto
    {
        public int AreaId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? MapCoordinates { get; set; }
    }

    public class CreateAreaDto
    {
        [Required(ErrorMessage = "Tên khu vực / kệ sách không được để trống")]
        [MaxLength(255, ErrorMessage = "Tên khu vực tối đa 255 ký tự")]
        [Display(Name = "Tên khu vực / Kệ sách")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500, ErrorMessage = "Tọa độ bản đồ tối đa 500 ký tự")]
        [Display(Name = "Tọa độ bản đồ (JSON/String)")]
        public string? MapCoordinates { get; set; }
    }

    public class UpdateAreaDto
    {
        [Required(ErrorMessage = "Tên khu vực / kệ sách không được để trống")]
        [MaxLength(255, ErrorMessage = "Tên khu vực tối đa 255 ký tự")]
        [Display(Name = "Tên khu vực / Kệ sách")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500, ErrorMessage = "Tọa độ bản đồ tối đa 500 ký tự")]
        [Display(Name = "Tọa độ bản đồ (JSON/String)")]
        public string? MapCoordinates { get; set; }
    }
}
