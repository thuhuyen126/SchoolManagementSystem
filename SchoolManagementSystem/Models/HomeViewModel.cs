namespace SchoolManagementSystem.Models
{
    public class HomeViewModel
    {
        public IEnumerable<CourseViewModel> Courses { get; set; } = Array.Empty<CourseViewModel>();
        public IEnumerable<SchoolClassViewModel> SchoolClasses { get; set; } = Array.Empty<SchoolClassViewModel>();

    }
}
