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
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, string> _decryptionKeys = new();

    public static string GetDecryptionKey(int sessionId)
    {
        return _decryptionKeys.GetOrAdd(sessionId, _ => Guid.NewGuid().ToString("N"));
    }

    private static string EncryptAes(string plainText, string keyString)
    {
        byte[] key = System.Text.Encoding.UTF8.GetBytes(keyString.PadRight(32).Substring(0, 32));
        byte[] iv = System.Text.Encoding.UTF8.GetBytes(keyString.PadRight(16).Substring(0, 16));
        using var aes = System.Security.Cryptography.Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var ms = new System.IO.MemoryStream();
        using (var cs = new System.Security.Cryptography.CryptoStream(ms, encryptor, System.Security.Cryptography.CryptoStreamMode.Write))
        {
            using (var sw = new System.IO.StreamWriter(cs))
            {
                sw.Write(plainText);
            }
        }
        return Convert.ToBase64String(ms.ToArray());
    }

    // POST /api/student/login
    [HttpPost("login")]
    public async Task<IActionResult> Login(StudentLoginDto dto)
    {
        var username = (dto.StudentId ?? "").Trim();
        var password = (dto.Password ?? "").Trim();

        var activeStudents = await db.Students.Where(s => s.IsActive).ToListAsync();
        var student = activeStudents.FirstOrDefault(s => 
            (s.StudentId.Equals(username, StringComparison.OrdinalIgnoreCase) || 
             s.FullName.Equals(username, StringComparison.OrdinalIgnoreCase)) && 
            s.Password.Equals(password, StringComparison.OrdinalIgnoreCase));

        if (student is null) return Unauthorized(new { error = "Invalid student credentials or inactive account." });
        return Ok(new StudentAdminDto(student.Id, student.FullName, student.StudentId, student.IsActive, student.Password));
    }

    // GET /api/student/active-session/{examId}
    // Public endpoint — students poll this to detect when admin opens a room for their exam
    [HttpGet("active-session/{examId}")]
    public async Task<IActionResult> GetActiveSession(int examId)
    {
        var session = await db.ExamSessions
            .Include(s => s.Exam)
            .Where(s => s.ExamId == examId && s.IsActive)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync();

        if (session is null) return Ok(new { found = false });

        return Ok(new
        {
            found      = true,
            id         = session.Id,
            examId     = session.ExamId,
            examTitle  = session.Exam?.Title ?? "",
            sessionCode = session.SessionCode,
            isActive   = session.IsActive,
            isStarted  = session.IsStarted
        });
    }

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

        var se = new StudentExam { StudentId = student.Id, SessionId = session.Id, DeviceId = dto.DeviceId, DeviceName = dto.DeviceName };
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

    // GET /api/student/{studentExamId}/questions/secure
    [HttpGet("{studentExamId}/questions/secure")]
    public async Task<IActionResult> GetQuestionsSecure(int studentExamId)
    {
        var se = await db.StudentExams.Include(x => x.Session).ThenInclude(s => s!.Exam).ThenInclude(e => e!.Questions)
            .FirstOrDefaultAsync(x => x.Id == studentExamId);

        if (se is null) return NotFound();
        if (se.IsSubmitted) return BadRequest("Exam already submitted.");

        var exam = se.Session!.Exam!;
        var shuffled = QuestionShuffler.ShuffleAll(exam.Questions, exam.ShuffleQuestions, exam.ShuffleOptions, se.Id);
        
        var json = System.Text.Json.JsonSerializer.Serialize(shuffled);
        var key = GetDecryptionKey(se.SessionId);
        var encrypted = EncryptAes(json, key);
        
        return Ok(new { encryptedQuestions = encrypted });
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

        // Update device status
        var device = await db.Devices.FirstOrDefaultAsync(d => d.DeviceId == dto.DeviceId);
        if (device is not null)
        {
            device.LastSeen = DateTime.UtcNow;
            device.IsOnline = true;
            await db.SaveChangesAsync();
        }

        await NotifyAdmin(se.Session!.SessionCode, se.SessionId, dto);

        var isStarted = se.Session?.IsStarted ?? false;
        var decKey = isStarted ? GetDecryptionKey(se.SessionId) : "";

        return Ok(new { 
            broadcastMessage = SessionsController.GetBroadcast(se.SessionId),
            isStarted = isStarted,
            decryptionKey = decKey
        });
    }

    [HttpPost("device")]
    public async Task<IActionResult> RegisterDevice(DeviceRegistrationDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.DeviceId)) return BadRequest();
        
        var device = await db.Devices.FirstOrDefaultAsync(d => d.DeviceId == dto.DeviceId);
        if (device is null)
        {
            device = new Device
            {
                DeviceId = dto.DeviceId,
                DeviceName = dto.DeviceName,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                LastSeen = DateTime.UtcNow,
                IsOnline = true,
                BatteryLevel = dto.BatteryLevel,
                StudentId = string.IsNullOrWhiteSpace(dto.StudentId) ? "Awaiting Login" : dto.StudentId
            };
            db.Devices.Add(device);
        }
        else
        {
            device.DeviceName = dto.DeviceName;
            device.LastSeen = DateTime.UtcNow;
            device.IsOnline = true;
            device.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? device.IpAddress;
            device.BatteryLevel = dto.BatteryLevel;
            device.StudentId = string.IsNullOrWhiteSpace(dto.StudentId) ? "Awaiting Login" : dto.StudentId;
        }

        await db.SaveChangesAsync();
        // Notify admin about device count update
        await hub.Clients.All.SendAsync("DeviceUpdate");
        return Ok();
    }

    [HttpGet("devices")]
    public async Task<IActionResult> GetDevices()
    {
        var threshold = DateTime.UtcNow.AddSeconds(-12);
        var devices = await db.Devices.ToListAsync();
        bool changed = false;
        foreach (var d in devices)
        {
            if (d.LastSeen < threshold && d.IsOnline)
            {
                d.IsOnline = false;
                changed = true;
            }
        }
        if (changed)
        {
            await db.SaveChangesAsync();
        }

        var dtos = new List<DeviceDto>();
        foreach (var d in devices)
        {
            string studentName = "Awaiting Login";
            string examTitle = "—";
            string examStatus = "Idle";

            if (!string.IsNullOrWhiteSpace(d.StudentId) && d.StudentId != "Awaiting Login")
            {
                var student = await db.Students
                    .Include(s => s.StudentExams)
                    .ThenInclude(se => se.Session)
                    .ThenInclude(session => session!.Exam)
                    .FirstOrDefaultAsync(s => s.StudentId == d.StudentId);
                
                if (student is not null)
                {
                    studentName = student.FullName;
                    var studentExam = student.StudentExams
                        .OrderByDescending(se => se.JoinedAt)
                        .FirstOrDefault();

                    if (studentExam is not null)
                    {
                        examTitle = studentExam.Session?.Exam?.Title ?? "—";
                        if (studentExam.IsSubmitted)
                        {
                            examStatus = "Submitted";
                        }
                        else if (studentExam.Session?.IsStarted == true)
                        {
                            examStatus = "Examining";
                        }
                        else
                        {
                            examStatus = "Waiting Room";
                        }
                    }
                    else
                    {
                        examStatus = "Selecting Exam";
                    }
                }
            }

            dtos.Add(new DeviceDto(
                d.DeviceId,
                d.DeviceName,
                d.IpAddress,
                d.LastSeen,
                d.IsOnline,
                d.BatteryLevel,
                d.StudentId,
                studentName,
                examTitle,
                examStatus
            ));
        }

        return Ok(dtos);
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
        var cutoff = DateTime.UtcNow.AddSeconds(-30);

        var studentExams = await db.StudentExams
            .Include(se => se.Student)
            .Include(se => se.Answers)
            .Where(se => se.SessionId == sessionId)
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

        var students = studentExams.Select(se => {
            var studentId = se.Student?.StudentId ?? "";
            deviceMap.TryGetValue(studentId, out var device);

            bool isOnline = device != null && device.LastSeen > cutoff && device.IsOnline;
            string status = se.IsSubmitted ? "submitted" : (isOnline ? "online" : "disconnected");

            var dto = new StudentStatusDto(
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
                device?.DeviceId ?? ""
            );

            // Override with current heartbeat if applicable
            if (heartbeat is not null && se.Id == heartbeat.StudentExamId)
            {
                dto = dto with
                {
                    CurrentQuestion = heartbeat.CurrentQuestion,
                    BatteryLevel = heartbeat.BatteryLevel,
                    IsOnline = heartbeat.IsOnline,
                    ConnectionState = heartbeat.ConnectionState,
                    DeviceName = heartbeat.DeviceName,
                    DeviceId = heartbeat.DeviceId
                };
            }

            return dto;
        }).ToList();

        await ExamHub.NotifyStudentUpdate(hub, sessionCode, students);
    }
}
