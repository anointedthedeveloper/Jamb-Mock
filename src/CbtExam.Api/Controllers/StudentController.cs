using CbtExam.Api.Hubs;
using CbtExam.Api.Services;
using CbtExam.Data;
using CbtExam.Shared.DTOs;
using CbtExam.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CbtExam.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StudentController(AppDbContext db, IHubContext<ExamHub> hub, SnapshotExportService exports) : ControllerBase
{
    // POST /api/student/join
    [HttpPost("join")]
    public async Task<IActionResult> Join(JoinExamDto dto)
    {
        var session = await db.ExamSessions
            .Include(s => s.Exam)
            .FirstOrDefaultAsync(s => s.SessionCode == dto.SessionCode && s.IsActive);

        if (session is null) return BadRequest("Invalid or inactive session code.");

        // Find or create student
        var student = await db.Students.FirstOrDefaultAsync(s => s.StudentId == dto.StudentId)
            ?? new Student { FullName = dto.FullName, StudentId = dto.StudentId };

        if (student.Id == 0)
        {
            db.Students.Add(student);
            await db.SaveChangesAsync();
        }

        // Check if already joined
        var existing = await db.StudentExams.FirstOrDefaultAsync(se => se.StudentId == student.Id && se.SessionId == session.Id);
        if (existing is not null)
            return Ok(new JoinResultDto(existing.Id, session.Id, session.ExamId, session.Exam!.Title, session.Exam.DurationMinutes));

        var se = new StudentExam { StudentId = student.Id, SessionId = session.Id };
        db.StudentExams.Add(se);
        await db.SaveChangesAsync();

        await NotifyAdmin(session.SessionCode, session.Id);
        return Ok(new JoinResultDto(se.Id, session.Id, session.ExamId, session.Exam!.Title, session.Exam.DurationMinutes));
    }

    // GET /api/student/{studentExamId}/questions
    [HttpGet("{studentExamId}/questions")]
    public async Task<IActionResult> GetQuestions(int studentExamId)
    {
        var se = await db.StudentExams.Include(x => x.Session).ThenInclude(s => s!.Exam).ThenInclude(e => e!.Questions)
            .FirstOrDefaultAsync(x => x.Id == studentExamId);

        if (se is null) return NotFound();
        if (se.IsSubmitted) return BadRequest("Exam already submitted.");

        var exam = se.Session!.Exam!;
        var shuffled = QuestionShuffler.ShuffleAll(exam.Questions, exam.ShuffleQuestions, exam.ShuffleOptions, se.Id);
        return Ok(shuffled);
    }

    // GET /api/student/{studentExamId}/progress
    [HttpGet("{studentExamId}/progress")]
    public async Task<IActionResult> GetProgress(int studentExamId)
    {
        var se = await db.StudentExams
            .Include(x => x.Answers)
            .FirstOrDefaultAsync(x => x.Id == studentExamId);
        if (se is null) return NotFound();

        var answers = se.Answers
            .Select(a => new AnswerSubmitDto(a.QuestionId, a.SelectedAnswer))
            .ToList();
        return Ok(new StudentProgressDto(se.Id, se.IsSubmitted, answers));
    }

    // POST /api/student/progress
    [HttpPost("progress")]
    public async Task<IActionResult> SaveProgress(ProgressSaveDto dto)
    {
        var se = await db.StudentExams
            .Include(x => x.Session)
            .Include(x => x.Answers)
            .FirstOrDefaultAsync(x => x.Id == dto.StudentExamId);
        if (se is null) return NotFound();
        if (se.IsSubmitted) return BadRequest("Exam already submitted.");

        var existing = se.Answers.FirstOrDefault(a => a.QuestionId == dto.QuestionId);
        if (existing is null)
        {
            db.Answers.Add(new Answer
            {
                StudentExamId = se.Id,
                QuestionId = dto.QuestionId,
                SelectedAnswer = dto.SelectedAnswer,
                IsCorrect = false
            });
        }
        else
        {
            existing.SelectedAnswer = dto.SelectedAnswer;
        }

        await db.SaveChangesAsync();
        await exports.ExportAllAsync();
        await NotifyAdmin(se.Session!.SessionCode, se.SessionId);
        return Ok();
    }

    // POST /api/student/submit
    [HttpPost("submit")]
    public async Task<IActionResult> Submit(ExamSubmitDto dto)
    {
        var se = await db.StudentExams
            .Include(x => x.Session).ThenInclude(s => s!.Exam).ThenInclude(e => e!.Questions)
            .Include(x => x.Answers)
            .FirstOrDefaultAsync(x => x.Id == dto.StudentExamId);

        if (se is null) return NotFound();
        if (se.IsSubmitted) return Ok(new SubmitResultDto(se.Score, se.Session!.Exam!.Questions.Count, 0));

        var questions = se.Session!.Exam!.Questions.ToDictionary(q => q.Id);
        int score = 0;

        foreach (var ans in dto.Answers.DistinctBy(a => a.QuestionId))
        {
            if (!questions.TryGetValue(ans.QuestionId, out var q)) continue;
            bool correct = q.CorrectAnswer.Equals(ans.SelectedAnswer, StringComparison.OrdinalIgnoreCase);
            if (correct) score++;

            var existing = se.Answers.FirstOrDefault(a => a.QuestionId == ans.QuestionId);
            if (existing is null)
            {
                db.Answers.Add(new Answer
                {
                    StudentExamId = se.Id,
                    QuestionId = ans.QuestionId,
                    SelectedAnswer = ans.SelectedAnswer,
                    IsCorrect = correct
                });
                continue;
            }

            existing.SelectedAnswer = ans.SelectedAnswer;
            existing.IsCorrect = correct;
        }

        se.IsSubmitted = true;
        se.SubmittedAt = DateTime.UtcNow;
        se.Score = score;
        await db.SaveChangesAsync();
        await exports.ExportAllAsync();

        var total = questions.Count;
        await NotifyAdmin(se.Session.SessionCode, se.SessionId);
        return Ok(new SubmitResultDto(score, total, total == 0 ? 0 : Math.Round((double)score / total * 100, 1)));
    }

    // POST /api/student/tabswitch
    [HttpPost("tabswitch")]
    public async Task<IActionResult> TabSwitch(TabSwitchDto dto)
    {
        var se = await db.StudentExams.Include(x => x.Session).FirstOrDefaultAsync(x => x.Id == dto.StudentExamId);
        if (se is null) return NotFound();
        se.TabSwitchCount++;
        await db.SaveChangesAsync();
        await NotifyAdmin(se.Session!.SessionCode, se.SessionId);
        return Ok();
    }

    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat(DeviceHeartbeatDto dto)
    {
        var se = await db.StudentExams.Include(x => x.Session).FirstOrDefaultAsync(x => x.Id == dto.StudentExamId);
        if (se is null) return NotFound();
        await NotifyAdmin(se.Session!.SessionCode, se.SessionId, dto);
        return Ok();
    }

    [HttpPost("snapshot")]
    public async Task<IActionResult> Snapshot(SnapshotDto dto)
    {
        if (dto.StudentExamId <= 0 || string.IsNullOrWhiteSpace(dto.ImageBase64)) return BadRequest();
        try
        {
            await exports.SaveSnapshotAsync(dto.StudentExamId, dto.ImageBase64);
            return Ok();
        }
        catch
        {
            return BadRequest("Invalid snapshot payload.");
        }
    }

    private async Task NotifyAdmin(string sessionCode, int sessionId, DeviceHeartbeatDto? heartbeat = null)
    {
        var students = await db.StudentExams
            .Include(se => se.Student)
            .Where(se => se.SessionId == sessionId)
            .Select(se => new StudentStatusDto(
                se.Id, se.Student!.FullName, se.Student.StudentId, se.JoinedAt, se.IsSubmitted, se.TabSwitchCount,
                se.Answers.Count, se.Answers.Count, 0, !se.IsSubmitted, se.IsSubmitted ? "submitted" : "online", ""))
            .ToListAsync();
        if (heartbeat is not null)
        {
            students = students
                .Select(s => s.StudentExamId == heartbeat.StudentExamId
                    ? s with
                    {
                        CurrentQuestion = heartbeat.CurrentQuestion,
                        BatteryLevel = heartbeat.BatteryLevel,
                        IsOnline = heartbeat.IsOnline,
                        ConnectionState = heartbeat.ConnectionState,
                        DeviceName = heartbeat.DeviceName
                    }
                    : s)
                .ToList();
        }
        await ExamHub.NotifyStudentUpdate(hub, sessionCode, students);
    }
}
