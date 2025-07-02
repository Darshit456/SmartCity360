using AuthService.DTOs;
using AuthService.Models;

namespace AuthService.Services
{
    public interface IAuthService
    {
        Task<AuthResponseDto?> RegisterUserAsync(RegisterUserDto registerDto);
        Task<AuthResponseDto?> LoginUserAsync(LoginUserDto loginDto);
        string GenerateJwtToken(User user);
        string HashPassword(string password);
        bool VerifyPassword(string password, string hashedPassword);
    }
}