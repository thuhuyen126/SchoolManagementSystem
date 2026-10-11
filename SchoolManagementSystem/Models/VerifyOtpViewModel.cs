using System.ComponentModel.DataAnnotations;

namespace SchoolManagementSystem.Models
{
    public class VerifyOtpViewModel
    {
        public string Email { get; set; }

        public bool RememberMe { get; set; }

        [Required(ErrorMessage = "Please enter the 6-digit OTP sent to your email.")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP must be exactly 6 digits.")]
        [RegularExpression("^[0-9]{6}$", ErrorMessage = "OTP must contain exactly 6 digits.")]
        public string Code { get; set; }
    }
}
