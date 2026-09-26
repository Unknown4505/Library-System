using BookKiosk.Domain.Entities;
using BookKiosk.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookKiosk.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class MembersController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public MembersController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Tra cứu thông tin thành viên theo số điện thoại
    /// </summary>
    [HttpGet("{phoneNumber}")]
    public async Task<IActionResult> GetMemberByPhone(string phoneNumber)
    {
        var member = await _context.Members.FirstOrDefaultAsync(m => m.PhoneNumber == phoneNumber);
        if (member == null) return NotFound("Không tìm thấy thành viên.");
        return Ok(member);
    }

    /// <summary>
    /// Lấy lịch sử điểm của thành viên
    /// </summary>
    [HttpGet("{id:int}/point-history")]
    public async Task<IActionResult> GetPointHistory(int id)
    {
        var history = await _context.PointTransactions
            .Where(p => p.MemberId == id)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
        return Ok(history);
    }

    /// <summary>
    /// Tạo mới thành viên
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateMember([FromBody] Member member)
    {
        if (await _context.Members.AnyAsync(m => m.PhoneNumber == member.PhoneNumber))
        {
            return BadRequest("Số điện thoại đã tồn tại.");
        }

        member.Points = 0; // Luôn khởi tạo 0 điểm
        _context.Members.Add(member);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetMemberByPhone), new { phoneNumber = member.PhoneNumber }, member);
    }

    /// <summary>
    /// Cập nhật thông tin thành viên (không cho phép sửa điểm trực tiếp ở đây)
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateMember(int id, [FromBody] Member updated)
    {
        var member = await _context.Members.FindAsync(id);
        if (member == null) return NotFound("Không tìm thấy thành viên.");

        // Nếu đổi số điện thoại, phải check trùng
        if (member.PhoneNumber != updated.PhoneNumber && await _context.Members.AnyAsync(m => m.PhoneNumber == updated.PhoneNumber))
        {
            return BadRequest("Số điện thoại mới đã được sử dụng.");
        }

        member.FullName = updated.FullName;
        member.PhoneNumber = updated.PhoneNumber;

        await _context.SaveChangesAsync();
        return Ok(member);
    }
}
