using System.ComponentModel.DataAnnotations;

namespace SchoolManagementSystem.Models
{
    public class RecoverPasswordViewModel
    {
        // Email is required
        [Required(ErrorMessage = "Email is required.")]
        // Validates that the input is a valid email address format
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; set; }
    }
}
