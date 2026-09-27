using AutoMapper;
using BookKiosk.Application.DTOs.Common;
using BookKiosk.Application.DTOs.Members;
using BookKiosk.Application.Interfaces.Repositories;
using BookKiosk.Domain.Entities;
using System.Collections.Generic;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberRepository _memberRepository;
    private readonly IMapper _mapper;

    public MemberService(IUnitOfWork unitOfWork, IMemberRepository memberRepository, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _memberRepository = memberRepository;
        _mapper = mapper;
    }

    public async Task<ApiResponseDto<MemberDto>> GetMemberByPhoneAsync(string phoneNumber)
    {
        var member = await _memberRepository.GetByPhoneAsync(phoneNumber);
        if (member == null) return ApiResponseDto<MemberDto>.Error("NOT_FOUND", "Không tìm thấy thành viên.");
        return ApiResponseDto<MemberDto>.Ok(_mapper.Map<MemberDto>(member));
    }

    public async Task<ApiResponseDto<IEnumerable<PointTransaction>>> GetPointHistoryAsync(int id)
    {
        var history = await _memberRepository.GetPointHistoryAsync(id);
        return ApiResponseDto<IEnumerable<PointTransaction>>.Ok(history);
    }

    public async Task<ApiResponseDto<MemberDto>> CreateMemberAsync(CreateMemberDto request)
    {
        if (await _memberRepository.CheckPhoneExistsAsync(request.PhoneNumber))
            return ApiResponseDto<MemberDto>.Error("BAD_REQUEST", "Số điện thoại đã tồn tại.");

        var member = _mapper.Map<Member>(request);
        member.Points = 0;
        await _memberRepository.AddAsync(member);
        await _unitOfWork.SaveChangesAsync();
        
        return ApiResponseDto<MemberDto>.Ok(_mapper.Map<MemberDto>(member));
    }

    public async Task<ApiResponseDto<MemberDto>> UpdateMemberAsync(int id, UpdateMemberDto request)
    {
        var member = await _memberRepository.GetByIdAsync(id);
        if (member == null) return ApiResponseDto<MemberDto>.Error("NOT_FOUND", "Không tìm thấy thành viên.");

        _mapper.Map(request, member);
        await _unitOfWork.SaveChangesAsync();
        return ApiResponseDto<MemberDto>.Ok(_mapper.Map<MemberDto>(member));
    }
}
