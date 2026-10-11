using System.Data;
using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Data.Entities;
using SchoolManagementSystem.Helpers;

/// <summary>
/// Seeds the approved, isolated test dataset when explicitly invoked.
/// This class is intentionally not registered or called during application startup.
/// </summary>
public class TestSeedDb
{
    private readonly SchoolDbContext _context;
    private readonly IUserHelper _userHelper;

    public TestSeedDb(SchoolDbContext context, IUserHelper userHelper)
    {
        _context = context;
        _userHelper = userHelper;
    }

    public async Task SeedAsync()
    {
        var testPassword = Environment.GetEnvironmentVariable("SCHOOL_TEST_PASSWORD");
        if (string.IsNullOrWhiteSpace(testPassword))
        {
            throw new InvalidOperationException("SCHOOL_TEST_PASSWORD must be set before seeding the test dataset.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            await SeedDatasetAsync(testPassword);
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task SeedDatasetAsync(string testPassword)
    {
        if (await _context.Users.AnyAsync() ||
            await _context.SchoolClasses.AnyAsync() ||
            await _context.Subjects.AnyAsync() ||
            await _context.Courses.AnyAsync() ||
            await _context.Teachers.AnyAsync() ||
            await _context.Employees.AnyAsync() ||
            await _context.Students.AnyAsync() ||
            await _context.Grades.AnyAsync() ||
            await _context.Attendances.AnyAsync() ||
            await _context.Payments.AnyAsync())
        {
            throw new InvalidOperationException("The test dataset can only be seeded into an empty database.");
        }

        await _userHelper.CheckRoleAsync("Admin");
        await _userHelper.CheckRoleAsync("Student");
        await _userHelper.CheckRoleAsync("Teacher");
        await _userHelper.CheckRoleAsync("Employee");

        await CreateUserAsync("admin.test@school.local", "Test", "Admin", "Admin", testPassword);
        var studentUser = await CreateUserAsync("student.test@school.local", "Test", "Student", "Student", testPassword);
        var teacherUser = await CreateUserAsync("teacher.test@school.local", "Test", "Teacher", "Teacher", testPassword);
        var employeeUser = await CreateUserAsync("employee.test@school.local", "Test", "Employee", "Employee", testPassword);

        var schoolClass = new SchoolClass
        {
            ClassName = "Test Class",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddMonths(6)
        };
        var subject = new Subject
        {
            Name = "Test Subject",
            Description = "Test dataset subject",
            Credits = 1,
            TotalClasses = 1
        };
        var course = new Course
        {
            Name = "Test Course",
            Description = "Test dataset course",
            Duration = 1,
            IsActive = true
        };

        _context.SchoolClasses.Add(schoolClass);
        _context.Subjects.Add(subject);
        _context.Courses.Add(course);
        await _context.SaveChangesAsync();

        schoolClass.CourseId = course.Id;
        _context.CourseSubjects.Add(new CourseSubject { CourseId = course.Id, SubjectId = subject.Id });

        var student = new Student
        {
            UserId = studentUser.Id,
            FirstName = studentUser.FirstName,
            LastName = studentUser.LastName,
            EnrollmentDate = DateTime.UtcNow,
            Status = StudentStatus.Active,
            SchoolClassId = schoolClass.Id
        };
        var teacher = new Teacher
        {
            UserId = teacherUser.Id,
            FirstName = teacherUser.FirstName,
            LastName = teacherUser.LastName,
            HireDate = DateTime.UtcNow,
            Status = TeacherStatus.Active,
            AcademicDegree = AcademicDegree.None
        };
        var employee = new Employee
        {
            UserId = employeeUser.Id,
            FirstName = employeeUser.FirstName,
            LastName = employeeUser.LastName,
            Department = Department.Administration,
            HireDate = DateTime.UtcNow,
            Status = EmployeeStatus.Active,
            PhoneNumber = "1234567890"
        };

        _context.Students.Add(student);
        _context.Teachers.Add(teacher);
        _context.Employees.Add(employee);
        await _context.SaveChangesAsync();

        _context.TeacherSubjects.Add(new TeacherSubject { TeacherId = teacher.Id, SubjectId = subject.Id });
        _context.TeacherSchoolClasses.Add(new TeacherSchoolClass { TeacherId = teacher.Id, SchoolClassId = schoolClass.Id });
        _context.Grades.Add(new Grade
        {
            StudentId = student.Id,
            SubjectId = subject.Id,
            Value = 10,
            EvaluationDate = DateTime.UtcNow
        });
        _context.Attendances.Add(new Attendance
        {
            StudentId = student.Id,
            SubjectId = subject.Id,
            Description = "Present",
            Date = DateTime.UtcNow
        });
        _context.Payments.Add(new Payment
        {
            StudentId = student.Id,
            Amount = 1,
            PaymentDate = DateTime.UtcNow,
            Status = "Paid",
            TransactionId = "TEST-TRANSACTION",
            PaymentMethod = "Test"
        });

        await _context.SaveChangesAsync();
    }

    private async Task<User> CreateUserAsync(string email, string firstName, string lastName, string role, string testPassword)
    {
        var user = new User
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            UserName = email,
            EmailConfirmed = true,
            DateCreated = DateTime.UtcNow
        };

        var result = await _userHelper.AddUserAsync(user, testPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(error =>
                $"{error.Code}: {error.Description}"));

            throw new InvalidOperationException(
                $"Could not create the test {role} user. Identity errors: {errors}");
        }

        var roleResult = await _userHelper.AddUserToRoleAsync(user, role);
        if (!roleResult.Succeeded || !await _userHelper.IsUserInRoleAsync(user, role))
        {
            throw new InvalidOperationException($"Could not assign the test {role} role to its user.");
        }

        return user;
    }
}
