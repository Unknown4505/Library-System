using BookKiosk.Application.DTOs.Common;
using BookKiosk.Application.DTOs.Users;
using BookKiosk.Application.Interfaces.Repositories;
using BookKiosk.Domain.Entities;
using BookKiosk.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace BookKiosk.Application.Services;

public interface IUserService
{
    Task<PaginatedResultDto<UserDto>> GetUsersAsync(UserFilterDto filter);
    Task<UserDto?> GetUserByIdAsync(int id);
    Task<UserDto> CreateUserAsync(CreateUserDto dto);
    Task<UserDto> UpdateUserAsync(int id, UpdateUserDto dto);
    Task<UserDto> UpdateUserStatusAsync(int id, bool isActive, int currentAdminId);
    Task ResetPasswordAsync(int id, string newPassword);
}

public class UserService : IUserService
{
    private static readonly Regex UsernameRegex = new(@"^[a-zA-Z0-9_]{3,50}$", RegexOptions.Compiled);
    private static readonly Regex PasswordRegex = new(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$", RegexOptions.Compiled);

    // SemaphoreSlim đồng bộ hóa in-process cho các thao tác tác động tới bất biến activeAdmins >= 1
    private static readonly SemaphoreSlim _adminLifecycleLock = new(1, 1);

    public static bool IsUniqueConstraintViolation(Exception ex)
    {
        var current = ex;
        while (current != null)
        {
            var typeName = current.GetType().Name;

            // 1. SQL Server: SqlException Number 2601 hoặc 2627
            if (typeName == "SqlException")
            {
                var numberProp = current.GetType().GetProperty("Number");
                if (numberProp?.GetValue(current) is int number && (number == 2601 || number == 2627))
                {
                    return true;
                }
            }

            // 2. SQLite: ErrorCode 19
            if (typeName == "SqliteException")
            {
                var codeProp = current.GetType().GetProperty("SqliteErrorCode");
                if (codeProp?.GetValue(current) is int code && code == 19 &&
                    current.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // 3. Fallback message inspection cho mocked/simulated exceptions
            var msg = current.Message;
            if (!string.IsNullOrEmpty(msg) &&
                (msg.Contains("2601") ||
                 msg.Contains("2627") ||
                 msg.Contains("IX_Users_Username", StringComparison.OrdinalIgnoreCase) ||
                 msg.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
                 msg.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }

    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UserService(IUserRepository userRepository, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<PaginatedResultDto<UserDto>> GetUsersAsync(UserFilterDto filter)
    {
        // Chuẩn hóa Clamp tham số phân trang chống tràn và giá trị âm
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 10 : (filter.PageSize > 100 ? 100 : filter.PageSize);

        var (items, totalCount) = await _userRepository.GetUsersPagedAsync(
            page, pageSize, filter.Keyword, filter.Role, filter.IsActive);

        return new PaginatedResultDto<UserDto>
        {
            Items = items.Select(u => new UserDto
            {
                UserId = u.UserId,
                Username = u.Username,
                FullName = u.FullName,
                Role = u.Role,
                IsActive = u.IsActive
            }).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<UserDto?> GetUserByIdAsync(int id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
        {
            return null;
        }

        return new UserDto
        {
            UserId = user.UserId,
            Username = user.Username,
            FullName = user.FullName,
            Role = user.Role,
            IsActive = user.IsActive
        };
    }

    public async Task<UserDto> CreateUserAsync(CreateUserDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Username) || !UsernameRegex.IsMatch(dto.Username.Trim()))
        {
            throw new ArgumentException("Tên đăng nhập không đúng định dạng (chỉ gồm 3-50 ký tự chữ, số và dấu gạch dưới).");
        }

        if (string.IsNullOrWhiteSpace(dto.Password) || !PasswordRegex.IsMatch(dto.Password))
        {
            throw new ArgumentException("Mật khẩu phải có tối thiểu 8 ký tự, bao gồm ít nhất một chữ hoa, một chữ thường và một chữ số.");
        }

        if (string.IsNullOrWhiteSpace(dto.FullName) || dto.FullName.Trim().Length < 2 || dto.FullName.Trim().Length > 100)
        {
            throw new ArgumentException("Họ và tên phải từ 2 đến 100 ký tự.");
        }

        if (!Enum.IsDefined(typeof(UserRole), dto.Role))
        {
            throw new ArgumentException("Vai trò người dùng không hợp lệ.");
        }

        var exists = await _userRepository.ExistsByUsernameAsync(dto.Username);
        if (exists)
        {
            throw new InvalidOperationException("Tên đăng nhập đã tồn tại trong hệ thống.");
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password, 11);

        var user = new User
        {
            Username = dto.Username.Trim(),
            PasswordHash = passwordHash,
            FullName = dto.FullName.Trim(),
            Role = dto.Role,
            IsActive = true
        };

        try
        {
            await _userRepository.AddAsync(user);
            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex) when (IsUniqueConstraintViolation(ex))
        {
            // Bắt vi phạm Unique Index trong SQL Server khi hai request cùng gửi đồng thời
            throw new InvalidOperationException("Tên đăng nhập đã tồn tại trong hệ thống.", ex);
        }

        return new UserDto
        {
            UserId = user.UserId,
            Username = user.Username,
            FullName = user.FullName,
            Role = user.Role,
            IsActive = user.IsActive
        };
    }

    public async Task<UserDto> UpdateUserAsync(int id, UpdateUserDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName) || dto.FullName.Trim().Length < 2 || dto.FullName.Trim().Length > 100)
        {
            throw new ArgumentException("Họ và tên phải từ 2 đến 100 ký tự.");
        }

        if (!Enum.IsDefined(typeof(UserRole), dto.Role))
        {
            throw new ArgumentException("Vai trò người dùng không hợp lệ.");
        }

        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy người dùng với ID = {id}.");
        }

        // Demote Guard: Ngăn chặn hạ quyền Admin duy nhất đang hoạt động
        if (user.Role == UserRole.Admin && user.IsActive && dto.Role != UserRole.Admin)
        {
            await _adminLifecycleLock.WaitAsync();
            try
            {
                await _unitOfWork.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                try
                {
                    var activeAdmins = await _userRepository.CountActiveAdminsAsync();
                    if (activeAdmins <= 1)
                    {
                        throw new InvalidOperationException("Hệ thống bắt buộc phải có ít nhất một tài khoản Quản trị viên (Admin) đang hoạt động.");
                    }

                    user.FullName = dto.FullName.Trim();
                    user.Role = dto.Role;

                    await _userRepository.UpdateAsync(user);
                    await _unitOfWork.SaveChangesAsync();
                    await _unitOfWork.CommitAsync();
                }
                catch
                {
                    await _unitOfWork.RollbackAsync();
                    throw;
                }
            }
            finally
            {
                _adminLifecycleLock.Release();
            }
        }
        else
        {
            user.FullName = dto.FullName.Trim();
            user.Role = dto.Role;

            await _userRepository.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();
        }

        return new UserDto
        {
            UserId = user.UserId,
            Username = user.Username,
            FullName = user.FullName,
            Role = user.Role,
            IsActive = user.IsActive
        };
    }

    public async Task<UserDto> UpdateUserStatusAsync(int id, bool isActive, int currentAdminId)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy người dùng với ID = {id}.");
        }

        // Chốt chặn 1: Self-Deactivation Guard (Ngăn Admin tự vô hiệu hóa tài khoản của chính mình)
        if (currentAdminId == id && !isActive)
        {
            throw new ArgumentException("Bạn không thể tự vô hiệu hóa tài khoản của chính mình.");
        }

        // Chốt chặn 2: Last Active Admin Guard với giao dịch Serializable và Semaphore Lock chống Race Condition
        if (user.Role == UserRole.Admin && user.IsActive && !isActive)
        {
            await _adminLifecycleLock.WaitAsync();
            try
            {
                await _unitOfWork.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                try
                {
                    var activeAdmins = await _userRepository.CountActiveAdminsAsync();
                    if (activeAdmins <= 1)
                    {
                        throw new InvalidOperationException("Hệ thống bắt buộc phải có ít nhất một tài khoản Quản trị viên (Admin) đang hoạt động.");
                    }

                    user.IsActive = false;
                    await _userRepository.UpdateAsync(user);
                    await _unitOfWork.SaveChangesAsync();
                    await _unitOfWork.CommitAsync();
                }
                catch
                {
                    await _unitOfWork.RollbackAsync();
                    throw;
                }
            }
            finally
            {
                _adminLifecycleLock.Release();
            }
        }
        else
        {
            user.IsActive = isActive;
            await _userRepository.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();
        }

        return new UserDto
        {
            UserId = user.UserId,
            Username = user.Username,
            FullName = user.FullName,
            Role = user.Role,
            IsActive = user.IsActive
        };
    }

    public async Task ResetPasswordAsync(int id, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || !PasswordRegex.IsMatch(newPassword))
        {
            throw new ArgumentException("Mật khẩu mới phải có tối thiểu 8 ký tự, bao gồm ít nhất một chữ hoa, một chữ thường và một chữ số.");
        }

        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy người dùng với ID = {id}.");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, 11);
        await _userRepository.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();
    }
}
