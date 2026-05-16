namespace CbtExam.Shared.DTOs;

// --- Exam DTOs ---
public record ExamCreateDto(string Title, string Subject, int DurationMinutes, bool ShuffleQuestions, bool ShuffleOptions, string AccessPassword = "");
public record ExamDto(int Id, string Title, string Subject, int DurationMinutes, bool ShuffleQuestions, bool ShuffleOptions, string AccessPassword, DateTime CreatedAt, int QuestionCount)
{
    // Year is parsed from Subject if stored as "Subject|Year"
    public string Year => Subject.Contains('|') ? Subject.Split('|')[1] : string.Empty;
    public string SubjectDisplay => Subject.Contains('|') ? Subject.Split('|')[0] : Subject;
}

// --- Question DTOs ---
public record QuestionCreateDto(int QuestionNumber, string Text, List<string> Options, string CorrectAnswer);
public record QuestionDto(int Id, int ExamId, int QuestionNumber, string Text, string OptionsJson, string CorrectAnswer);

// --- Session DTOs ---
public record SessionStartDto(int ExamId);
public record SessionDto(int Id, int ExamId, string ExamTitle, string SessionCode, DateTime StartedAt, bool IsActive, int StudentCount);

// --- Student DTOs ---
public record JoinExamDto(string SessionCode, string FullName, string StudentId);
public record JoinResultDto(int StudentExamId, int SessionId, int ExamId, string ExamTitle, int DurationMinutes);
public record JoinRequestDto(int StudentExamId, int SessionId, string FullName, string StudentId, DateTime RequestedAt, bool IsApproved);

// --- Question for student (shuffled) ---
public record ShuffledQuestionDto(int QuestionId, int QuestionNumber, string Text, List<string> Options, int CorrectIndex);

// --- Answer submission ---
public record AnswerSubmitDto(int QuestionId, string SelectedAnswer);
public record ExamSubmitDto(int StudentExamId, List<AnswerSubmitDto> Answers);
public record SubmitResultDto(int Score, int Total, double Percentage);
public record ProgressSaveDto(int StudentExamId, int QuestionId, string SelectedAnswer);
public record StudentProgressDto(int StudentExamId, bool IsSubmitted, List<AnswerSubmitDto> Answers);

// --- Monitor ---
public record StudentStatusDto(
    int StudentExamId,
    string FullName,
    string StudentId,
    DateTime JoinedAt,
    bool IsSubmitted,
    int TabSwitchCount,
    int AnsweredCount,
    int CurrentQuestion,
    int BatteryLevel,
    bool IsOnline,
    string ConnectionState,
    string DeviceName = "",
    string DeviceId = "");

// --- Results ---
public record ResultDto(int StudentExamId, string FullName, string StudentId, int Score, int Total, double Percentage, DateTime? SubmittedAt)
{
    public bool Passed => Percentage >= 50;
}

// --- Activity log ---
public record ActivityLogDto(int StudentExamId, string Activity, DateTime Timestamp);

// --- Tab switch report ---
public record TabSwitchDto(int StudentExamId);
public record DeviceHeartbeatDto(int StudentExamId, int CurrentQuestion, int BatteryLevel, bool IsOnline, string ConnectionState, string DeviceName = "", string DeviceId = "");
public record SnapshotDto(int StudentExamId, string ImageBase64);

// --- Student admin management ---
public record StudentAdminDto(int Id, string FullName, string StudentId, bool IsActive, string Password = "");
public record StudentUpsertDto(int? Id, string FullName, string StudentId, bool IsActive, string? Password = null);
public record StudentPasswordUpdateDto(int StudentId, string NewPassword);
public record StudentBulkRowDto(string FullName, string Username, string Password);

// --- Question Bank DTOs ---
public record QuestionBankCreateDto(string Subject, int Year, int QuestionNumber, string Text, List<string> Options, string CorrectAnswer);
public record QuestionBankDto(int Id, string Subject, int Year, int QuestionNumber, string Text, string OptionsJson, string CorrectAnswer);
public record QuestionBankSubjectYearDto(string Subject, List<int> Years, int QuestionCount);

// --- Exam generation from question bank ---
public record ExamGenerateDto(string Title, int DurationMinutes, bool ShuffleQuestions, bool ShuffleOptions, string AccessPassword, List<QuestionBankSubjectYearDto> Subjects);

// --- Notifications ---
public record NotificationDto(string Title, string Message, DateTime CreatedAt, string Level);
