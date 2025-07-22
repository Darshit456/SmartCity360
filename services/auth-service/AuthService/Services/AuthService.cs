using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AuthService.Data;
using AuthService.DTOs;
using AuthService.Models;
using AuthService.Services;
using BCrypt.Net;

namespace AuthService.Services
{
    public class AuthService : IAuthService
    {
        private readonly SmartCityDbContext _context;
        private readonly ILogger<AuthService> _logger;
        private readonly IJwtService _jwtService;

        public AuthService(SmartCityDbContext context, ILogger<AuthService> logger, IJwtService jwtService)
        {
            _context = context;
            _logger = logger;
            _jwtService = jwtService; 
        }

        public async Task<AuthResponseDto?> RegisterUserAsync(RegisterUserDto registerDto)
{
    try
    {
        _logger.LogInformation("Starting registration for: {FirstName} {LastName} ({Email})",
            registerDto.FirstName, registerDto.LastName, registerDto.Email);

        // Generate username from FirstName + LastName
        var generatedUsername = $"{registerDto.FirstName} {registerDto.LastName}".Trim();
        _logger.LogInformation("Generated username: '{Username}'", generatedUsername);

        // Check total users first
        var totalUsers = await _context.Users.CountAsync();
        _logger.LogInformation("Current users in database: {Count}", totalUsers);

        // Check for existing email
        var existingEmail = await _context.Users
            .Where(u => u.Email == registerDto.Email)
            .FirstOrDefaultAsync();

        if (existingEmail != null)
        {
            _logger.LogWarning("Email already exists: {Email}", registerDto.Email);
            return null;
        }

        // Check for existing username
        var existingUsername = await _context.Users
            .Where(u => u.Username == generatedUsername)
            .FirstOrDefaultAsync();

        if (existingUsername != null)
        {
            _logger.LogWarning("Username already exists: {Username}", generatedUsername);
            return null;
        }

        _logger.LogInformation("No conflicts found. Creating user...");

        // Create new user - don't set CreatedAt/UpdatedAt, let database handle it
        var user = new User
        {
            Username = generatedUsername,
            Email = registerDto.Email,
            PasswordHash = HashPassword(registerDto.Password),
            Role = registerDto.Role ?? "Citizen", // Default to Citizen if null
            FirstName = registerDto.FirstName,
            LastName = registerDto.LastName,
            IsActive = true
            // Don't set CreatedAt/UpdatedAt - let database defaults handle it
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User created successfully with ID: {UserId}", user.Id);

        // Generate JWT token
        var token = _jwtService.GenerateToken(user);
        return new AuthResponseDto
        {
            Token = token,
            Username = user.Username,
            Role = user.Role,
            ExpiresAt = DateTime.UtcNow.AddHours(24) // Use UTC for consistency
        };
    }
    catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("CK_User_Role") == true)
    {
        _logger.LogError("Invalid role provided: {Role}", registerDto.Role);
        throw new ArgumentException("Invalid role. Valid roles are: Admin, CityPlanner, Citizen");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Registration failed for {FirstName} {LastName}: {Error}",
            registerDto.FirstName, registerDto.LastName, ex.Message);
        return null;
    }
}

        public async Task<AuthResponseDto?> LoginUserAsync(LoginUserDto loginDto)
        {
            try
            {
                _logger.LogInformation("Login attempt for email: {Email}", loginDto.Email);

                // Find user by email
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == loginDto.Email && u.IsActive);

                if (user == null)
                {
                    _logger.LogWarning("Login failed: User not found or inactive for email {Email}", loginDto.Email);
                    return null;
                }

                // Verify password
                if (!VerifyPassword(loginDto.Password, user.PasswordHash))
                {
                    _logger.LogWarning("Login failed: Invalid password for {Email}", loginDto.Email);
                    return null;
                }

                // Update last login time with UTC
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Generate JWT token
                var token = _jwtService.GenerateToken(user);

                _logger.LogInformation("Login successful for user: {Username}", user.Username);

                return new AuthResponseDto
                {
                    Token = token,
                    Username = user.Username,
                    Role = user.Role,
                    ExpiresAt = DateTime.UtcNow.AddHours(24) // Use UTC
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error for {Email}: {Error}", loginDto.Email, ex.Message);
                return null;
            }
        }

        
        public string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt());
        }

        public bool VerifyPassword(string password, string hashedPassword)
        {
            return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
        }
    }
}