using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BookKiosk.Application.DTOs.Auth;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ChangeOwnPasswordDto
{
    [Required, MaxLength(1024)]
    public string CurrentPassword { get; set; } = string.Empty;
    [Required, MinLength(8), MaxLength(72)]
    public string NewPassword { get; set; } = string.Empty;
}
