using System.ComponentModel.DataAnnotations;

namespace SchoolManagementSystem.Models
{
    public class ChangeFirstPasswordViewModel
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password setup token is required.")]
        public string Token { get; set; }

        [Required(ErrorMessage = "New password is required.")]
        [DataType(DataType.Password)]
        // Ép độ dài từ 10 đến 100 ký tự
        [StringLength(100, MinimumLength = 10, ErrorMessage = "Password must be at least 10 characters long.")]
        // Ép quy tắc: Hoa, thường, số, ký tự đặc biệt
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).+$",
            ErrorMessage = "Password must have at least 1 uppercase letter, 1 lowercase letter, 1 number, and 1 special character.")]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "Password confirmation is required.")]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
        public string ConfirmNewPassword { get; set; }
    }
}
