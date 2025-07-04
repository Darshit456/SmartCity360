using Microsoft.AspNetCore.Mvc;
using AuthService.DTOs;
using AuthService.Services;
using Microsoft.EntityFrameworkCore;
using AuthService.Data;

namespace AuthService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;
        private readonly SmartCityDbContext _context;

        public AuthController(IAuthService authService, ILogger<AuthController> logger, SmartCityDbContext context)
        {
            _authService = authService;
            _logger = logger;
            _context = context;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterUserDto registerDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _authService.RegisterUserAsync(registerDto);

                if (result == null)
                {
                    return BadRequest(new { message = "User registration failed. Username or email may already exist." });
                }

                _logger.LogInformation("User {FirstName} {LastName} registered successfully",
                    registerDto.FirstName, registerDto.LastName);
                return Ok(new { message = "User registered successfully", user = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user registration for {FirstName} {LastName}",
                    registerDto.FirstName, registerDto.LastName);
                return StatusCode(500, new { message = "Internal server error during registration" });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginUserDto loginDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _authService.LoginUserAsync(loginDto);

                if (result == null)
                {
                    return Unauthorized(new { message = "Invalid email or password" });
                }

                _logger.LogInformation("User with email {Email} logged in successfully", loginDto.Email);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for {Email}", loginDto.Email);
                return StatusCode(500, new { message = "Internal server error during login" });
            }
        }

        [HttpGet("test-auth")]
        public IActionResult TestAuth()
        {
            return Ok(new { message = "AuthController is working", timestamp = DateTime.Now });
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers()
        {
            try
            {
                var users = await _context.Users
                    .Select(u => new
                    {
                        u.Id,
                        u.Username,
                        u.Email,
                        u.FirstName,
                        u.LastName,
                        u.Role,
                        u.IsActive,
                        u.CreatedAt
                    })
                    .ToListAsync();

                return Ok(users);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving users", details = ex.Message });
            }
        }
    }
}