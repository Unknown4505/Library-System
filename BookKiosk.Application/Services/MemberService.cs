using AutoMapper;
using BookKiosk.Application.DTOs.Common;
using BookKiosk.Application.DTOs.Members;
using BookKiosk.Domain.Entities;
using BookKiosk.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BookKiosk.Application.Services;

public interface IMemberService
{
    Task<ApiResponseDto<MemberDto>> GetMemberByPhoneAsync(string phoneNumber);
    Task<ApiResponseDto<IEnumerable<PointTransaction>>> GetPointHistoryAsync(int id);
    Task<ApiResponseDto<MemberDto>> CreateMemberAsync(CreateMemberDto request);
    Task<ApiResponseDto<MemberDto>> UpdateMemberAsync(int id, UpdateMemberDto request);
}

public class MemberService : IMemberService
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;

    public MemberService(ApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<ApiResponseDto<MemberDto>> GetMemberByPhoneAsync(string phoneNumber)
    {
        var member = await _context.Members.FirstOrDefaultAsync(m => m.PhoneNumber == phoneNumber);
        if (member == null) return ApiResponseDto<MemberDto>.Error("NOT_FOUND", "Không tìm thấy thành viên.");
        return ApiResponseDto<MemberDto>.Ok(_mapper.Map<MemberDto>(member));
    }

    public async Task<ApiResponseDto<IEnumerable<PointTransaction>>> GetPointHistoryAsync(int id)
    {
        var history = await _context.PointTransactions
            .Where(p => p.MemberId == id)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
        return ApiResponseDto<IEnumerable<PointTransaction>>.Ok(history);
    }

    public async Task<ApiResponseDto<MemberDto>> CreateMemberAsync(CreateMemberDto request)
    {
        if (await _context.Members.AnyAsync(m => m.PhoneNumber == request.PhoneNumber))
            return ApiResponseDto<MemberDto>.Error("BAD_REQUEST", "Số điện thoại đã tồn tại.");

        var member = _mapper.Map<Member>(request);
        member.Points = 0;
        _context.Members.Add(member);
        await _context.SaveChangesAsync();
        
        return ApiResponseDto<MemberDto>.Ok(_mapper.Map<MemberDto>(member));
    }

    public async Task<ApiResponseDto<MemberDto>> UpdateMemberAsync(int id, UpdateMemberDto request)
    {
        var member = await _context.Members.FindAsync(id);
        if (member == null) return ApiResponseDto<MemberDto>.Error("NOT_FOUND", "Không tìm thấy thành viên.");

        _mapper.Map(request, member);
        await _context.SaveChangesAsync();
        return ApiResponseDto<MemberDto>.Ok(_mapper.Map<MemberDto>(member));
    }
}
