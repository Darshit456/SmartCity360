using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdminService.Models;
using AdminService.DTOs;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace AdminService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")] // 🔒 Protect entire controller - Admin only
    public class AdminController : ControllerBase
    {
        private readonly AdminDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AdminController> _logger;
        private readonly IConfiguration _configuration;

        public AdminController(
            AdminDbContext context,
            IHttpClientFactory httpClientFactory,
            ILogger<AdminController> logger,
            IConfiguration configuration)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _configuration = configuration;
        }

        [HttpGet("health")]
        public IActionResult GetHealth()
        {
            try
            {
                // Get current user info from JWT token
                var currentUserId = GetCurrentUserId();
                var currentUserName = GetCurrentUserName();

                var canConnect = _context.Database.CanConnect();
                var response = new ApiResponseDto<object>
                {
                    Success = true,
                    Message = "AdminService is running",
                    Data = new
                    {
                        Status = "Healthy",
                        Database = canConnect ? "Connected" : "Disconnected",
                        Timestamp = DateTime.UtcNow,
                        Port = "5001",
                        AuthenticatedUser = currentUserName,
                        UserId = currentUserId
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
                return StatusCode(500, new ApiResponseDto<object>
                {
                    Success = false,
                    Message = "Health check failed",
                    Error = ex.Message
                });
            }
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers()
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                _logger.LogInformation($"Admin user {currentUserId} attempting to retrieve users from AuthService");

                var httpClient = _httpClientFactory.CreateClient();
                var authServiceUrl = _configuration["ServiceUrls:AuthService"];
                var requestUrl = $"{authServiceUrl}/api/auth/users";

                // Add JWT token to the request for AuthService
                var token = GetCurrentUserToken();
                if (!string.IsNullOrEmpty(token))
                {
                    httpClient.DefaultRequestHeaders.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                _logger.LogInformation($"Calling AuthService at: {requestUrl}");

                var response = await httpClient.GetAsync(requestUrl);

                if (response.IsSuccessStatusCode)
                {
                    var usersJson = await response.Content.ReadAsStringAsync();
                    var users = JsonSerializer.Deserialize<List<UserDto>>(usersJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    // Log admin activity with real user ID
                    await LogAdminActivity(
                        userId: currentUserId,
                        action: "Retrieved user list",
                        details: $"Successfully retrieved {users?.Count ?? 0} users from AuthService"
                    );

                    var apiResponse = new ApiResponseDto<List<UserDto>>
                    {
                        Success = true,
                        Message = "Users retrieved successfully",
                        Data = users
                    };

                    return Ok(apiResponse);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"AuthService returned {response.StatusCode}: {errorContent}");

                    return StatusCode(500, new ApiResponseDto<object>
                    {
                        Success = false,
                        Message = "Failed to retrieve users from AuthService",
                        Error = $"AuthService returned {response.StatusCode}"
                    });
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request to AuthService failed");
                return StatusCode(500, new ApiResponseDto<object>
                {
                    Success = false,
                    Message = "Service communication failed",
                    Error = "Cannot connect to AuthService. Please ensure AuthService is running."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error retrieving users");
                return StatusCode(500, new ApiResponseDto<object>
                {
                    Success = false,
                    Message = "An unexpected error occurred",
                    Error = ex.Message
                });
            }
        }

        [HttpGet("logs")]
        public async Task<IActionResult> GetAdminLogs()
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                _logger.LogInformation($"Admin user {currentUserId} retrieving admin logs");

                var logs = await _context.AdminLogs
                    .OrderByDescending(l => l.Timestamp)
                    .Take(50)
                    .Select(l => new AdminLogDto
                    {
                        Id = l.Id,
                        UserId = l.UserId,
                        Action = l.Action,
                        Details = l.Details,
                        IpAddress = l.IpAddress,
                        Timestamp = l.Timestamp
                    })
                    .ToListAsync();

                var response = new ApiResponseDto<List<AdminLogDto>>
                {
                    Success = true,
                    Message = $"Retrieved {logs.Count} admin logs",
                    Data = logs
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving admin logs");
                return StatusCode(500, new ApiResponseDto<object>
                {
                    Success = false,
                    Message = "Failed to retrieve admin logs",
                    Error = ex.Message
                });
            }
        }

        [HttpPost("settings")]
        public async Task<IActionResult> UpdateSystemSetting([FromBody] SystemSettingRequestDto request)
        {
            try
            {
                var currentUserId = GetCurrentUserId();

                // Input validation
                if (string.IsNullOrWhiteSpace(request.Key) || string.IsNullOrWhiteSpace(request.Value))
                {
                    return BadRequest(new ApiResponseDto<object>
                    {
                        Success = false,
                        Message = "Key and Value are required",
                        Error = "Invalid input"
                    });
                }

                _logger.LogInformation($"Admin user {currentUserId} updating system setting: {request.Key}");

                var setting = await _context.SystemSettings
                    .FirstOrDefaultAsync(s => s.Key == request.Key);

                if (setting == null)
                {
                    setting = new SystemSetting
                    {
                        Key = request.Key,
                        Value = request.Value,
                        Description = request.Description,
                        UpdatedBy = currentUserId, // Real user ID from JWT
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.SystemSettings.Add(setting);
                }
                else
                {
                    setting.Value = request.Value;
                    setting.Description = request.Description;
                    setting.UpdatedBy = currentUserId; // Real user ID from JWT
                    setting.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                // Log admin activity with real user ID
                await LogAdminActivity(
                    userId: currentUserId,
                    action: "Updated system setting",
                    details: $"Updated setting '{request.Key}' to '{request.Value}'"
                );

                var response = new ApiResponseDto<SystemSetting>
                {
                    Success = true,
                    Message = "System setting updated successfully",
                    Data = setting
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating system setting: {request.Key}");
                return StatusCode(500, new ApiResponseDto<object>
                {
                    Success = false,
                    Message = "Failed to update system setting",
                    Error = ex.Message
                });
            }
        }

        [HttpGet("settings")]
        public async Task<IActionResult> GetSystemSettings()
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                _logger.LogInformation($"Admin user {currentUserId} retrieving system settings");

                var settings = await _context.SystemSettings
                    .OrderBy(s => s.Key)
                    .ToListAsync();

                var response = new ApiResponseDto<List<SystemSetting>>
                {
                    Success = true,
                    Message = $"Retrieved {settings.Count} system settings",
                    Data = settings
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving system settings");
                return StatusCode(500, new ApiResponseDto<object>
                {
                    Success = false,
                    Message = "Failed to retrieve system settings",
                    Error = ex.Message
                });
            }
        }

        // 🔧 Helper methods to extract user info from JWT token
        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out int userId) ? userId : 0;
        }

        private string GetCurrentUserName()
        {
            return User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
        }

        private string GetCurrentUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";
        }

        private string GetCurrentUserToken()
        {
            var authHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
            if (authHeader != null && authHeader.StartsWith("Bearer "))
            {
                return authHeader.Substring("Bearer ".Length).Trim();
            }
            return string.Empty;
        }

        private async Task LogAdminActivity(int userId, string action, string details)
        {
            try
            {
                var adminLog = new AdminLog
                {
                    UserId = userId,
                    Action = action,
                    Details = details,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    Timestamp = DateTime.UtcNow
                };

                _context.AdminLogs.Add(adminLog);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Admin activity logged: {action} by user {userId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log admin activity");
                // Don't throw here - logging failure shouldn't break the main operation
            }
        }
    }
}