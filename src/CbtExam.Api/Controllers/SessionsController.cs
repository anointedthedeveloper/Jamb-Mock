using CbtExam.Data;
using CbtExam.Api.Services;
using CbtExam.Shared.DTOs;
using CbtExam.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CbtExam.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SessionsController(AppDbContext db, SnapshotExportService exports) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await db.ExamSessions
            .Include(s => s.Exam)
            .Select(s => new SessionDto(s.Id, s.ExamId, s.Exam!.Title, s.SessionCode, s.StartedAt, s.IsActive, s.StudentExams.Count, s.IsStarted))
            .ToListAsync());

    [HttpPost("start")]
    public async Task<IActionResult> Start(SessionStartDto dto)
    {
        var exam = await db.Exams.FindAsync(dto.ExamId);
        if (exam is null) return NotFound("Exam not found");

        // Multiple simultaneous sessions are allowed
        var session = new ExamSession
        {
            ExamId = dto.ExamId,
            SessionCode = GenerateCode(),
            StartedAt = DateTime.UtcNow,
            IsActive = true
        };
        db.ExamSessions.Add(session);
        await db.SaveChangesAsync();
        return Ok(new SessionDto(session.Id, session.ExamId, exam.Title, session.SessionCode, session.StartedAt, true, 0, false));
    }

    [HttpPost("{id}/begin")]
    public async Task<IActionResult> Begin(int id)
    {
        var session = await db.ExamSessions.FindAsync(id);
        if (session is null || !session.IsActive) return NotFound("Session not found or already ended.");
        session.IsStarted = true;
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("{id}/stop")]
    public async Task<IActionResult> Stop(int id)
    {
        var session = await db.ExamSessions.FindAsync(id);
        if (session is null) return NotFound();
        session.IsActive = false;
        session.EndedAt = DateTime.UtcNow;
        await AutoSubmitUnsubmittedAsync(session.Id);
        await db.SaveChangesAsync();
        await exports.ExportAllAsync();
        return Ok();
    }

    [HttpPost("end-all")]
    public async Task<IActionResult> EndAll()
    {
        var active = await db.ExamSessions.Where(s => s.IsActive).ToListAsync();
        if (active.Count == 0) return Ok(0);
        foreach (var session in active)
        {
            session.IsActive = false;
            session.EndedAt = DateTime.UtcNow;
            await AutoSubmitUnsubmittedAsync(session.Id);
        }

        await db.SaveChangesAsync();
        await exports.ExportAllAsync();
        return Ok(active.Count);
    }

    [HttpGet("{id}/students")]
    public async Task<IActionResult> GetStudents(int id)
    {
        var studentExams = await db.StudentExams
            .Include(se => se.Student)
            .Include(se => se.Answers)
            .Where(se => se.SessionId == id)
            .ToListAsync();

        var studentIds = studentExams.Select(se => se.Student!.StudentId).ToList();
        var devices = await db.Devices
            .Where(d => studentIds.Contains(d.StudentId))
            .ToListAsync();

        var threshold = DateTime.UtcNow.AddSeconds(-30);

        var result = studentExams.Select(se => {
            var device = devices.FirstOrDefault(d => d.StudentId == se.Student!.StudentId);
            bool isOnline = device != null && device.LastSeen >= threshold && device.IsOnline;
            string status = se.IsSubmitted ? "submitted" : (isOnline ? "online" : "disconnected");
            
            return new StudentStatusDto(
                se.Id, se.Student!.FullName, se.Student.StudentId,
                se.JoinedAt, se.IsSubmitted, se.TabSwitchCount,
                se.Answers.Count, 0, device?.BatteryLevel ?? 0, isOnline, status, 
                device?.DeviceName ?? "Unknown", 
                device?.DeviceId ?? "");
        }).ToList();

        return Ok(result);
    }

    [HttpGet("{id}/results")]
    public async Task<IActionResult> GetResults(int id) =>
        Ok(await db.StudentExams
            .Include(se => se.Student)
            .Where(se => se.SessionId == id && se.IsSubmitted)
            .Select(se => new ResultDto(
                se.Id, se.Student!.FullName, se.Student.StudentId,
                se.Score, se.Session!.Exam!.Questions.Count,
                se.Session.Exam.Questions.Count == 0 ? 0 : Math.Round((double)se.Score / se.Session.Exam.Questions.Count * 100, 1),
                se.SubmittedAt))
            .ToListAsync());

    [HttpPost("export")]
    public async Task<IActionResult> Export()
    {
        await exports.ExportAllAsync();
        return Ok(new { exportedAt = DateTime.UtcNow });
    }

    private static string GenerateCode() =>
        Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();

    private async Task AutoSubmitUnsubmittedAsync(int sessionId)
    {
        var studentExams = await db.StudentExams
            .Include(se => se.Session).ThenInclude(s => s!.Exam).ThenInclude(e => e!.Questions)
            .Include(se => se.Answers)
            .Where(se => se.SessionId == sessionId && !se.IsSubmitted)
            .ToListAsync();

        foreach (var se in studentExams)
        {
            var questions = se.Session!.Exam!.Questions.ToDictionary(q => q.Id);
            var score = se.Answers.Count(a => questions.TryGetValue(a.QuestionId, out var q) &&
                string.Equals(a.SelectedAnswer, q.CorrectAnswer, StringComparison.OrdinalIgnoreCase));
            se.Score = score;
            se.IsSubmitted = true;
            se.SubmittedAt = DateTime.UtcNow;
        }
    }
}
