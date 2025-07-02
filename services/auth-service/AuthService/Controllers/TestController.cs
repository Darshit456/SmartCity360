using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AuthService.Data;

namespace AuthService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly SmartCityDbContext _context;

        public TestController(SmartCityDbContext context)
        {
            _context = context;
        }

        [HttpGet("db-connection")]
        public async Task<IActionResult> TestDatabaseConnection()
        {
            try
            {
                // Test database connection
                await _context.Database.CanConnectAsync();

                // Count users in database
                var userCount = await _context.Users.CountAsync();

                return Ok(new
                {
                    status = "Connected",
                    message = "Database connection successful",
                    userCount = userCount,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    status = "Error",
                    message = ex.Message
                });
            }
        }
    }
}