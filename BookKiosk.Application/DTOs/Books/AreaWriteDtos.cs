using System.ComponentModel.DataAnnotations;

namespace BookKiosk.Application.DTOs.Books;

public sealed class CreateAreaDto
{
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? MapCoordinates { get; set; }
}

public sealed class UpdateAreaDto
{
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? MapCoordinates { get; set; }
}
