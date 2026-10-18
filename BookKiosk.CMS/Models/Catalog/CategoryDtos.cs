using System.ComponentModel.DataAnnotations;

namespace BookKiosk.CMS.Models.Catalog
{
    public class CategoryDto
    {
        public int CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class CreateCategoryDto
    {
        [Required(ErrorMessage = "Tên danh mục không được để trống")]
        [MaxLength(255, ErrorMessage = "Tên danh mục tối đa 255 ký tự")]
        [Display(Name = "Tên danh mục")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500, ErrorMessage = "Mô tả tối đa 500 ký tự")]
        [Display(Name = "Mô tả")]
        public string? Description { get; set; }
    }

    public class UpdateCategoryDto
    {
        [Required(ErrorMessage = "Tên danh mục không được để trống")]
        [MaxLength(255, ErrorMessage = "Tên danh mục tối đa 255 ký tự")]
        [Display(Name = "Tên danh mục")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500, ErrorMessage = "Mô tả tối đa 500 ký tự")]
        [Display(Name = "Mô tả")]
        public string? Description { get; set; }
    }
}
