using CbtExam.Data;
using CbtExam.Api.Services;
using CbtExam.Shared.DTOs;
using CbtExam.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace CbtExam.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SessionsController(AppDbContext db, SnapshotExportService exports) : ControllerBase
{
    private static readonly ConcurrentDictionary<int, string> _broadcasts = new();

    public static string GetBroadcast(int sessionId) =>
        _broadcasts.TryGetValue(sessionId, out var msg) ? msg : string.Empty;
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var sessions = await db.ExamSessions
            .Include(s => s.Exam)
            .ToListAsync();

        var result = sessions.Select(s => {
            _broadcasts.TryGetValue(s.Id, out var msg);
            return new SessionDto(s.Id, s.ExamId, s.Exam!.Title, s.SessionCode, s.StartedAt, s.IsActive, s.StudentExams.Count, s.IsStarted, msg ?? "");
        }).ToList();

        return Ok(result);
    }

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
        var cutoff = DateTime.UtcNow.AddSeconds(-30);

        var studentExams = await db.StudentExams
            .Include(se => se.Student)
            .Include(se => se.Answers)
            .Where(se => se.SessionId == id)
            .ToListAsync();

        var studentIds = studentExams
            .Select(se => se.Student?.StudentId)
            .Where(sid => !string.IsNullOrEmpty(sid))
            .Distinct()
            .ToList();

        var devices = await db.Devices
            .Where(d => studentIds.Contains(d.StudentId))
            .ToListAsync();

        var deviceMap = devices
            .GroupBy(d => d.StudentId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var result = studentExams.Select(se => {
            var studentId = se.Student?.StudentId ?? "";
            deviceMap.TryGetValue(studentId, out var device);
            
            bool isOnline = device != null && device.LastSeen > cutoff && device.IsOnline;
            string status = se.IsSubmitted ? "submitted" : (isOnline ? "online" : "disconnected");
            
            return new StudentStatusDto(
                se.Id, 
                se.Student?.FullName ?? "Unknown", 
                studentId,
                se.JoinedAt, 
                se.IsSubmitted, 
                se.TabSwitchCount,
                se.Answers.Count, 
                0, 
                device?.BatteryLevel ?? 0, 
                isOnline, 
                status, 
                device?.DeviceName ?? "Unknown", 
                device?.DeviceId ?? "");
        }).ToList();

        return Ok(result);
    }

    [HttpPost("{id}/broadcast")]
    public IActionResult Broadcast(int id, [FromBody] BroadcastDto dto)
    {
        _broadcasts[id] = dto.Message;
        return Ok();
    }

    [HttpGet("{id}/results")]
    public async Task<IActionResult> GetResults(int id)
    {
        var studentExams = await db.StudentExams
            .Include(se => se.Student)
            .Include(se => se.Answers).ThenInclude(a => a.Question)
            .Include(se => se.Session).ThenInclude(s => s!.Exam).ThenInclude(e => e!.Questions)
            .Where(se => se.SessionId == id && se.IsSubmitted)
            .ToListAsync();

        var result = studentExams.Select(se => {
            var breakdownGroup = se.Answers
                .Where(a => a.Question != null && !string.IsNullOrEmpty(a.Question.Subject))
                .GroupBy(a => a.Question!.Subject)
                .Select(g => $"{g.Key}: {g.Count(a => a.IsCorrect)}/{g.Count()}");

            var breakdownStr = string.Join(", ", breakdownGroup);
            if (string.IsNullOrEmpty(breakdownStr)) breakdownStr = "N/A";

            int totalQs = se.Session?.Exam?.Questions?.Count ?? 0;

            return new ResultDto(
                se.Id, se.Student!.FullName, se.Student.StudentId,
                se.Score, totalQs,
                totalQs == 0 ? 0 : Math.Round((double)se.Score / totalQs * 100, 1),
                se.SubmittedAt, breakdownStr);
        }).ToList();

        return Ok(result);
    }

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
