using SchoolManagementSystem.Data.Entities;

namespace SchoolManagementSystem.Models
{
    public class StudentSubjectGradeViewModel
    {
        public Subject Subject { get; set; }
        public Grade? Grade { get; set; }
        public int StudentId { get; set; }
        public string StudentName { get; set; } 

    }
}
