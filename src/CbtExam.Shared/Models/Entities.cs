namespace CbtExam.Shared.Models;

public class Exam
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public bool ShuffleQuestions { get; set; } = true;
    public bool ShuffleOptions { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Question> Questions { get; set; } = new List<Question>();
    public ICollection<ExamSession> Sessions { get; set; } = new List<ExamSession>();
}

public class Question
{
    public int Id { get; set; }
    public int ExamId { get; set; }
    public int QuestionNumber { get; set; }
    public string Text { get; set; } = string.Empty;
    // Stored as JSON array string: ["A","B","C","D"]
    public string OptionsJson { get; set; } = "[]";
    public string CorrectAnswer { get; set; } = string.Empty;
    public Exam? Exam { get; set; }
}

public class ExamSession
{
    public int Id { get; set; }
    public int ExamId { get; set; }
    public string SessionCode { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public bool IsActive { get; set; }
    public Exam? Exam { get; set; }
    public ICollection<StudentExam> StudentExams { get; set; } = new List<StudentExam>();
}

public class Student
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string StudentId { get; set; } = string.Empty;  // username / login ID
    public string Password { get; set; } = "1234";
    public bool IsActive { get; set; } = true;
    public ICollection<StudentExam> StudentExams { get; set; } = new List<StudentExam>();
}

// Standalone question bank (subject + year, not tied to a specific exam)
public class QuestionBank
{
    public int Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public int Year { get; set; }
    public int QuestionNumber { get; set; }
    public string Text { get; set; } = string.Empty;
    public string OptionsJson { get; set; } = "[]";
    public string CorrectAnswer { get; set; } = string.Empty;
}

public class StudentExam
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int SessionId { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }
    public bool IsSubmitted { get; set; }
    public int Score { get; set; }
    public int TabSwitchCount { get; set; }
    public Student? Student { get; set; }
    public ExamSession? Session { get; set; }
    public ICollection<Answer> Answers { get; set; } = new List<Answer>();
}

public class Answer
{
    public int Id { get; set; }
    public int StudentExamId { get; set; }
    public int QuestionId { get; set; }
    public string SelectedAnswer { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public StudentExam? StudentExam { get; set; }
    public Question? Question { get; set; }
}
