using System.ComponentModel.DataAnnotations;

namespace AuthService.DTOs
{
    public class RegisterUserDto
    {
        [Required]
        [EmailAddress]
        [StringLength(200)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        [RoleValidation] // Custom validation attribute
        public string Role { get; set; } = "Citizen";

        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;
    }

    // Custom validation attribute for roles
    public class RoleValidationAttribute : ValidationAttribute
    {
        private static readonly string[] ValidRoles = { "Admin", "CityPlanner", "Citizen" };

        public override bool IsValid(object? value)
        {
            if (value is string role)
            {
                return ValidRoles.Contains(role, StringComparer.OrdinalIgnoreCase);
            }
            return false;
        }

        public override string FormatErrorMessage(string name)
        {
            return $"Role must be one of: {string.Join(", ", ValidRoles)}";
        }
    }
}