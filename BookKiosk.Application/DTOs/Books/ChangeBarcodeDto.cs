using System.ComponentModel.DataAnnotations;

namespace BookKiosk.Application.DTOs.Books;

public sealed class ChangeBarcodeRequestDto
{
    [Required] public string NewBarcode { get; set; } = string.Empty;
    [Required] public string Reason { get; set; } = string.Empty;
}

public sealed record ChangeBarcodeDto(int BookId, string OldBarcode, string NewBarcode,
    bool RequiresLabelReprint, DateTime ChangedAtUtc);
