namespace BookKiosk.Application.DTOs.Members;

public class MemberDto
{
    public int MemberId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int Points { get; set; }
}
