using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Data.Entities;

public class SchoolDbContext : IdentityDbContext<User>
{
    public DbSet<Course> Courses { get; set; }
    public DbSet<Student> Students { get; set; }
    public DbSet<Subject> Subjects { get; set; }
    public DbSet<SchoolClass> SchoolClasses { get; set; }
    public DbSet<Teacher> Teachers { get; set; }
    public DbSet<Employee> Employees { get; set; }
    public DbSet<Attendance> Attendances { get; set; }
    public DbSet<Grade> Grades { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<Alert> Alerts { get; set; }
    public DbSet<TeacherSubject> TeacherSubjects { get; set; }
    public DbSet<TeacherSchoolClass> TeacherSchoolClasses { get; set; }
    public DbSet<CourseSubject> CourseSubjects { get; set; }
    public DbSet<SystemAlert> SystemAlerts { get; set; }
    public DbSet<ActivityLog> ActivityLogs { get; set; }

    public SchoolDbContext(DbContextOptions<SchoolDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        //// Relacionamentos para SchoolClass
        //modelBuilder.Entity<Subject>()
        //    .HasOne(s => s.SchoolClass)
        //    .WithMany(sc => sc.Subjects)
        //    .HasForeignKey(s => s.SchoolClassId)
        //    .OnDelete(DeleteBehavior.Restrict); // Restringe a exclusão

        modelBuilder.Entity<Student>()
            .HasOne(s => s.SchoolClass)
            .WithMany(sc => sc.Students)
            .HasForeignKey(s => s.SchoolClassId)
            .OnDelete(DeleteBehavior.Restrict);

        // Relationship between Student and User
        modelBuilder.Entity<Student>()
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Many-to-many relationship between Teacher and Subject
        modelBuilder.Entity<TeacherSubject>()
            .HasKey(ts => new { ts.TeacherId, ts.SubjectId }); // Composite key

        modelBuilder.Entity<TeacherSubject>()
            .HasOne(ts => ts.Teacher)
            .WithMany(t => t.TeacherSubjects)
            .HasForeignKey(ts => ts.TeacherId)
            .OnDelete(DeleteBehavior.Cascade); // Automatically deletes associations when deleting a Teacher

        modelBuilder.Entity<TeacherSubject>()
            .HasOne(ts => ts.Subject)
            .WithMany(s => s.TeacherSubjects)
            .HasForeignKey(ts => ts.SubjectId)
            .OnDelete(DeleteBehavior.Restrict); // Restriction on exclusion from the subject


        // Configura o relacionamento muitos-para-muitos
        modelBuilder.Entity<CourseSubject>()
            .HasKey(cs => new { cs.CourseId, cs.SubjectId });

        modelBuilder.Entity<CourseSubject>()
            .HasOne(cs => cs.Course)
            .WithMany(c => c.CourseSubjects)
            .HasForeignKey(cs => cs.CourseId)
            .OnDelete(DeleteBehavior.Cascade); // Permitir exclusão em cascata

        modelBuilder.Entity<CourseSubject>()
            .HasOne(cs => cs.Subject)
            .WithMany(s => s.CourseSubjects)
            .HasForeignKey(cs => cs.SubjectId)
            .OnDelete(DeleteBehavior.Cascade); // Permitir exclusão em cascata

        modelBuilder.Entity<SchoolClass>()
            .HasOne(sc => sc.Course)
            .WithMany(c => c.SchoolClasses)
            .HasForeignKey(sc => sc.CourseId)
            .OnDelete(DeleteBehavior.SetNull); // Defina como nulo em vez de excluir


        // Relationship between Teacher and SchoolClass
        modelBuilder.Entity<TeacherSchoolClass>()
            .HasKey(tsc => new { tsc.TeacherId, tsc.SchoolClassId }); // Composite key

        modelBuilder.Entity<TeacherSchoolClass>()
            .HasOne(tsc => tsc.Teacher)
            .WithMany(t => t.TeacherSchoolClasses)
            .HasForeignKey(tsc => tsc.TeacherId)
            .OnDelete(DeleteBehavior.Cascade); // Automatically deletes associations when deleting a Teacher

        modelBuilder.Entity<TeacherSchoolClass>()
            .HasOne(tsc => tsc.SchoolClass)
            .WithMany(sc => sc.TeacherSchoolClasses)
            .HasForeignKey(tsc => tsc.SchoolClassId)
            .OnDelete(DeleteBehavior.Restrict); // Restriction on class exclusion
    }
}
