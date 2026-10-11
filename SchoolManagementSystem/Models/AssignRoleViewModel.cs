namespace SchoolManagementSystem.Models
{
    public class AssignRoleViewModel
    {
        public string UserId { get; set; }
        public string SelectedRole { get; set; }
        public List<string> Roles { get; set; } = new List<string>();
    }
}
