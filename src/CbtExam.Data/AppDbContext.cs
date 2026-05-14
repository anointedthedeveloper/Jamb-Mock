using CbtExam.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace CbtExam.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<ExamSession> ExamSessions => Set<ExamSession>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<StudentExam> StudentExams => Set<StudentExam>();
    public DbSet<Answer> Answers => Set<Answer>();
    public DbSet<QuestionBank> QuestionBank => Set<QuestionBank>();
    public DbSet<AdminConfig> AdminConfigs => Set<AdminConfig>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Exam>().HasMany(e => e.Questions).WithOne(q => q.Exam).HasForeignKey(q => q.ExamId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<Exam>().HasMany(e => e.Sessions).WithOne(s => s.Exam).HasForeignKey(s => s.ExamId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<ExamSession>().HasMany(s => s.StudentExams).WithOne(se => se.Session).HasForeignKey(se => se.SessionId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<Student>().HasMany(s => s.StudentExams).WithOne(se => se.Student).HasForeignKey(se => se.StudentId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<StudentExam>().HasMany(se => se.Answers).WithOne(a => a.StudentExam).HasForeignKey(a => a.StudentExamId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<ExamSession>().HasIndex(s => s.SessionCode).IsUnique();
        mb.Entity<Student>().Property(s => s.Password).HasDefaultValue("1234");
        mb.Entity<Student>().Property(s => s.IsActive).HasDefaultValue(true);
        mb.Entity<QuestionBank>().HasIndex(q => new { q.Subject, q.Year });
    }
}
