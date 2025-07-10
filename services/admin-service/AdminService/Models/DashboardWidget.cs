using System.ComponentModel.DataAnnotations;

namespace AdminService.Models
{
    public class DashboardWidget
    {
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [StringLength(50)]
        public string WidgetType { get; set; } = string.Empty;

        [StringLength(2000)]
        public string Configuration { get; set; } = string.Empty; // JSON configuration

        public int Position { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}