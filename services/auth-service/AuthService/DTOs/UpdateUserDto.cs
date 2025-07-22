using System.ComponentModel.DataAnnotations;

namespace AuthService.DTOs
{
    public class UpdateUserDto
    {
        [EmailAddress]
        [StringLength(200)]
        public string? Email { get; set; }

        [StringLength(100)]
        public string? FirstName { get; set; }

        [StringLength(100)]
        public string? LastName { get; set; }

        [StringLength(50)]
        public string? Role { get; set; }

        [MinLength(6)]
        public string? NewPassword { get; set; }

        // Add this field
        public bool? IsActive { get; set; }
    }
}