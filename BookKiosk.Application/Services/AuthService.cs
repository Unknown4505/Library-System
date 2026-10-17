using BookKiosk.Application.DTOs.Auth;
using BookKiosk.Application.Interfaces.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace BookKiosk.Application.Services;

public interface IAuthService
{
    Task<LoginResponseDto> LoginAsync(LoginRequestDto request);
}

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IConfiguration _configuration;

    public AuthService(IUserRepository userRepository, IConfiguration configuration)
    {
        _userRepository = userRepository;
        _configuration = configuration;
    }

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ArgumentException("Tên đăng nhập và mật khẩu không được để trống.");
        }

        var user = await _userRepository.GetByUsernameAsync(request.Username.Trim());
        if (user == null || !user.IsActive)
        {
            throw new UnauthorizedAccessException("Tài khoản hoặc mật khẩu không chính xác hoặc tài khoản đã bị khóa.");
        }

        bool isPasswordValid = false;
        try
        {
            isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        }
        catch
        {
            isPasswordValid = false;
        }

        if (!isPasswordValid)
        {
            throw new UnauthorizedAccessException("Tài khoản hoặc mật khẩu không chính xác hoặc tài khoản đã bị khóa.");
        }

        var jwtKey = _configuration["Jwt:Key"] ?? "Day_La_Mot_Khoa_Bi_Mat_Dai_Nhat_Co_The_123456789";
        var issuer = _configuration["Jwt:Issuer"] ?? "BookKiosk";
        var audience = _configuration["Jwt:Audience"] ?? "BookKioskUser";

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiresAt = DateTime.UtcNow.AddHours(request.RememberMe ? 24 * 7 : 8);

        var roleName = user.Role.ToString();

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.GivenName, user.FullName),
            new Claim(ClaimTypes.Role, roleName),
            new Claim("role", roleName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        var tokenHandler = new JwtSecurityTokenHandler();
        var accessToken = tokenHandler.WriteToken(token);

        return new LoginResponseDto
        {
            AccessToken = accessToken,
            ExpiresAt = expiresAt,
            FullName = user.FullName,
            Role = roleName
        };
    }
}
