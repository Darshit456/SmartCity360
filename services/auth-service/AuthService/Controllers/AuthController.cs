using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using AuthService.DTOs;
using AuthService.Services;
using Microsoft.EntityFrameworkCore;
using AuthService.Data;
using Microsoft.AspNetCore.Authorization;

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
        
        // PUBLIC ENDPOINTS
        
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
        
        // PROTECTED ENDPOINTS - READ OPERATIONS
        
        [Authorize(Roles = "Admin")]
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
        
        [Authorize(Roles = "Admin")]
        [HttpGet("users/{id}")]
        public async Task<IActionResult> GetUserById(int id)
        {
            try
            {
                var user = await _context.Users
                    .Where(u => u.Id == id)
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
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                return Ok(user);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving user", details = ex.Message });
            }
        }
        
        // PROTECTED ENDPOINTS - UPDATE OPERATIONS
        
        [Authorize]
        [HttpPut("users/{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserDto updateDto)
        {
            try
            {
                // Get current user ID from JWT token
                var currentUserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;
        
                if (!int.TryParse(currentUserIdClaim, out int currentUserId))
                {
                    return Unauthorized(new { message = "Invalid token" });
                }

                // Check if user can update this profile (Admin or own profile)
                if (currentUserRole != "Admin" && currentUserId != id)
                {
                    return Forbid("You can only update your own profile");
                }

                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                // Validate email uniqueness if email is being updated
                if (!string.IsNullOrEmpty(updateDto.Email) && updateDto.Email != user.Email)
                {
                    var emailExists = await _context.Users
                        .AnyAsync(u => u.Email == updateDto.Email && u.Id != id);
                    
                    if (emailExists)
                    {
                        return BadRequest(new { message = "Email already exists" });
                    }
                }

                // Validate role if being updated
                if (!string.IsNullOrEmpty(updateDto.Role) && currentUserRole == "Admin")
                {
                    var validRoles = new[] { "Admin", "CityPlanner", "Citizen" };
                    if (!validRoles.Contains(updateDto.Role))
                    {
                        return BadRequest(new { message = "Invalid role. Valid roles are: Admin, CityPlanner, Citizen" });
                    }
                }

                // Update allowed fields
                if (!string.IsNullOrEmpty(updateDto.FirstName))
                    user.FirstName = updateDto.FirstName;
        
                if (!string.IsNullOrEmpty(updateDto.LastName))
                    user.LastName = updateDto.LastName;
        
                if (!string.IsNullOrEmpty(updateDto.Email))
                    user.Email = updateDto.Email;

                // Only Admin can update role
                if (!string.IsNullOrEmpty(updateDto.Role) && currentUserRole == "Admin")
                    user.Role = updateDto.Role;

                // Only Admin can update IsActive status
                if (updateDto.IsActive.HasValue && currentUserRole == "Admin")
                    user.IsActive = updateDto.IsActive.Value;

                // Handle password update
                if (!string.IsNullOrEmpty(updateDto.NewPassword))
                {
                    user.PasswordHash = _authService.HashPassword(updateDto.NewPassword);
                }

                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("User {UserId} updated successfully", id);
                return Ok(new { message = "User updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating user", details = ex.Message });
            }
        }
        
        // PROTECTED ENDPOINTS - DELETE OPERATIONS
        
        [Authorize(Roles = "Admin")]
        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            try
            {
                // Get current user ID to prevent self-deletion
                var currentUserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(currentUserIdClaim, out int currentUserId) && currentUserId == id)
                {
                    return BadRequest(new { message = "You cannot delete your own account" });
                }

                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                // Soft delete - just deactivate user
                user.IsActive = false;
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("User {UserId} ({Username}) deactivated by Admin", id, user.Username);
                return Ok(new { message = "User deactivated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error deleting user", details = ex.Message });
            }
        }
    }
}