namespace SchoolManagementSystem.Models
{
    public class UserWithRoleViewModel
    {
        public string UserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string? Email { get; set; }
        public string Role { get; set; }
        public bool IsLocked { get; set; }
    }
}
