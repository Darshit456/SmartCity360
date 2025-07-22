using AuthService.Models;
using AuthService.DTOs;

namespace AuthService.Services;

public interface IJwtService
{
    string GenerateToken(User user);
}