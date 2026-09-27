using BookKiosk.Application.DTOs.Members;
using BookKiosk.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace BookKiosk.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class MembersController : ControllerBase
{
    private readonly IMemberService _memberService;

    public MembersController(IMemberService memberService)
    {
        _memberService = memberService;
    }

    [HttpGet("{phoneNumber}")]
    public async Task<IActionResult> GetMemberByPhone(string phoneNumber)
    {
        return Ok(await _memberService.GetMemberByPhoneAsync(phoneNumber));
    }

    [HttpGet("{id:int}/point-history")]
    public async Task<IActionResult> GetPointHistory(int id)
    {
        return Ok(await _memberService.GetPointHistoryAsync(id));
    }

    [HttpPost]
    public async Task<IActionResult> CreateMember([FromBody] CreateMemberDto request)
    {
        return Ok(await _memberService.CreateMemberAsync(request));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateMember(int id, [FromBody] UpdateMemberDto request)
    {
        return Ok(await _memberService.UpdateMemberAsync(id, request));
    }
}
