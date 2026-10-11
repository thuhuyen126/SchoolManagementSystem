using System.ComponentModel.DataAnnotations;

namespace SchoolManagementSystem.Models;

public sealed class ConfirmEmailViewModel
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string Token { get; set; } = string.Empty;
}
