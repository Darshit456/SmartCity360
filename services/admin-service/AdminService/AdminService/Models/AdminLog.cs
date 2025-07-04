using System.ComponentModel.DataAnnotations;

namespace AdminService.Models
{
    public class AdminLog
    {
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }  // Reference to user from AuthService

        [Required]
        [StringLength(200)]
        public string Action { get; set; } = string.Empty;

        [StringLength(1000)]
        public string Details { get; set; } = string.Empty;

        [StringLength(45)]
        public string IpAddress { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}