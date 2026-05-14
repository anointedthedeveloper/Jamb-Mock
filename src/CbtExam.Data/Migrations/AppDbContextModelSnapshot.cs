using CbtExam.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace CbtExam.Data.Migrations;

[DbContext(typeof(AppDbContext))]
partial class AppDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder mb)
    {
#pragma warning disable 612, 618
        mb.HasAnnotation("ProductVersion", "8.0.0");

        mb.Entity("CbtExam.Shared.Models.Exam", b =>
        {
            b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER");
            b.Property<DateTime>("CreatedAt").HasColumnType("TEXT");
            b.Property<int>("DurationMinutes").HasColumnType("INTEGER");
            b.Property<bool>("ShuffleOptions").HasColumnType("INTEGER");
            b.Property<bool>("ShuffleQuestions").HasColumnType("INTEGER");
            b.Property<string>("Subject").IsRequired().HasColumnType("TEXT");
            b.Property<string>("Title").IsRequired().HasColumnType("TEXT");
            b.HasKey("Id");
            b.ToTable("Exams");
        });

        mb.Entity("CbtExam.Shared.Models.Question", b =>
        {
            b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER");
            b.Property<int>("ExamId").HasColumnType("INTEGER");
            b.Property<string>("CorrectAnswer").IsRequired().HasColumnType("TEXT");
            b.Property<string>("OptionsJson").IsRequired().HasColumnType("TEXT");
            b.Property<int>("QuestionNumber").HasColumnType("INTEGER");
            b.Property<string>("Text").IsRequired().HasColumnType("TEXT");
            b.HasKey("Id");
            b.HasIndex("ExamId");
            b.ToTable("Questions");
        });

        mb.Entity("CbtExam.Shared.Models.ExamSession", b =>
        {
            b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER");
            b.Property<DateTime?>("EndedAt").HasColumnType("TEXT");
            b.Property<int>("ExamId").HasColumnType("INTEGER");
            b.Property<bool>("IsActive").HasColumnType("INTEGER");
            b.Property<string>("SessionCode").IsRequired().HasColumnType("TEXT");
            b.Property<DateTime>("StartedAt").HasColumnType("TEXT");
            b.HasKey("Id");
            b.HasIndex("SessionCode").IsUnique();
            b.ToTable("ExamSessions");
        });

        mb.Entity("CbtExam.Shared.Models.Student", b =>
        {
            b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER");
            b.Property<string>("FullName").IsRequired().HasColumnType("TEXT");
            b.Property<string>("StudentId").IsRequired().HasColumnType("TEXT");
            b.Property<string>("Password").IsRequired().HasDefaultValue("1234").HasColumnType("TEXT");
            b.Property<bool>("IsActive").HasDefaultValue(true).HasColumnType("INTEGER");
            b.HasKey("Id");
            b.ToTable("Students");
        });

        mb.Entity("CbtExam.Shared.Models.StudentExam", b =>
        {
            b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER");
            b.Property<bool>("IsSubmitted").HasColumnType("INTEGER");
            b.Property<DateTime>("JoinedAt").HasColumnType("TEXT");
            b.Property<int>("Score").HasColumnType("INTEGER");
            b.Property<int>("SessionId").HasColumnType("INTEGER");
            b.Property<DateTime?>("SubmittedAt").HasColumnType("TEXT");
            b.Property<int>("StudentId").HasColumnType("INTEGER");
            b.Property<int>("TabSwitchCount").HasColumnType("INTEGER");
            b.HasKey("Id");
            b.HasIndex("SessionId");
            b.HasIndex("StudentId");
            b.ToTable("StudentExams");
        });

        mb.Entity("CbtExam.Shared.Models.Answer", b =>
        {
            b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER");
            b.Property<bool>("IsCorrect").HasColumnType("INTEGER");
            b.Property<int>("QuestionId").HasColumnType("INTEGER");
            b.Property<string>("SelectedAnswer").IsRequired().HasColumnType("TEXT");
            b.Property<int>("StudentExamId").HasColumnType("INTEGER");
            b.HasKey("Id");
            b.HasIndex("QuestionId");
            b.HasIndex("StudentExamId");
            b.ToTable("Answers");
        });

        mb.Entity("CbtExam.Shared.Models.QuestionBank", b =>
        {
            b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER");
            b.Property<string>("Subject").IsRequired().HasColumnType("TEXT");
            b.Property<int>("Year").HasColumnType("INTEGER");
            b.Property<int>("QuestionNumber").HasColumnType("INTEGER");
            b.Property<string>("Text").IsRequired().HasColumnType("TEXT");
            b.Property<string>("OptionsJson").IsRequired().HasColumnType("TEXT");
            b.Property<string>("CorrectAnswer").IsRequired().HasColumnType("TEXT");
            b.HasKey("Id");
            b.HasIndex("Subject", "Year");
            b.ToTable("QuestionBank");
        });

        mb.Entity("CbtExam.Shared.Models.Question", b =>
        {
            b.HasOne("CbtExam.Shared.Models.Exam", "Exam").WithMany("Questions").HasForeignKey("ExamId").OnDelete(DeleteBehavior.Cascade).IsRequired();
            b.Navigation("Exam");
        });

        mb.Entity("CbtExam.Shared.Models.ExamSession", b =>
        {
            b.HasOne("CbtExam.Shared.Models.Exam", "Exam").WithMany("Sessions").HasForeignKey("ExamId").OnDelete(DeleteBehavior.Cascade).IsRequired();
            b.Navigation("Exam");
        });

        mb.Entity("CbtExam.Shared.Models.StudentExam", b =>
        {
            b.HasOne("CbtExam.Shared.Models.Student", "Student").WithMany("StudentExams").HasForeignKey("StudentId").OnDelete(DeleteBehavior.Cascade).IsRequired();
            b.HasOne("CbtExam.Shared.Models.ExamSession", "Session").WithMany("StudentExams").HasForeignKey("SessionId").OnDelete(DeleteBehavior.Cascade).IsRequired();
            b.Navigation("Student");
            b.Navigation("Session");
        });

        mb.Entity("CbtExam.Shared.Models.Answer", b =>
        {
            b.HasOne("CbtExam.Shared.Models.StudentExam", "StudentExam").WithMany("Answers").HasForeignKey("StudentExamId").OnDelete(DeleteBehavior.Cascade).IsRequired();
            b.HasOne("CbtExam.Shared.Models.Question", "Question").WithMany().HasForeignKey("QuestionId").OnDelete(DeleteBehavior.Cascade).IsRequired();
            b.Navigation("StudentExam");
            b.Navigation("Question");
        });

        mb.Entity("CbtExam.Shared.Models.Exam", b =>
        {
            b.Navigation("Questions");
            b.Navigation("Sessions");
        });

        mb.Entity("CbtExam.Shared.Models.Student", b => b.Navigation("StudentExams"));
        mb.Entity("CbtExam.Shared.Models.ExamSession", b => b.Navigation("StudentExams"));
        mb.Entity("CbtExam.Shared.Models.StudentExam", b => b.Navigation("Answers"));
#pragma warning restore 612, 618
    }
}
