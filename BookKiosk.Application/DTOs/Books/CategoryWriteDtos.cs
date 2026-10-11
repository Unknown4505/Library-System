using System.ComponentModel.DataAnnotations;

namespace BookKiosk.Application.DTOs.Books;

public sealed class CreateCategoryDto
{
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}

public sealed class UpdateCategoryDto
{
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}
