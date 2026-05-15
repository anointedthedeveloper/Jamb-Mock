using CbtExam.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CbtExam.Api.Services;

public sealed class SnapshotExportService
{
    private readonly AppDbContext _db;
    private readonly string _basePath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly SemaphoreSlim _exportLock = new(1, 1);

    public SnapshotExportService(AppDbContext db)
    {
        _db = db;
        _basePath = Path.Combine(AppContext.BaseDirectory, "database");
    }

    public async Task ExportAllAsync()
    {
        Directory.CreateDirectory(_basePath);
        var datedResultsDir = Path.Combine(_basePath, "results");
        var datedLogsDir = Path.Combine(_basePath, "logs");
        Directory.CreateDirectory(datedResultsDir);
        Directory.CreateDirectory(datedLogsDir);

        await WriteJsonAsync("students.json", await _db.Students.AsNoTracking().ToListAsync());
        await WriteJsonAsync("exams.json", await _db.Exams.AsNoTracking().ToListAsync());
        await WriteJsonAsync("exam-state.json", await _db.ExamSessions.AsNoTracking().ToListAsync());
        await WriteJsonAsync("results.json", await _db.StudentExams.AsNoTracking().Where(x => x.IsSubmitted).ToListAsync());

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        await WriteJsonAsync(Path.Combine("results", $"results_{stamp}.json"),
            await _db.StudentExams.AsNoTracking().Include(x => x.Student).Where(x => x.IsSubmitted).ToListAsync());
        await WriteJsonAsync(Path.Combine("logs", $"activity_{stamp}.json"),
            await _db.StudentExams.AsNoTracking().Select(x => new
            {
                x.Id,
                x.StudentId,
                x.SessionId,
                x.JoinedAt,
                x.SubmittedAt,
                x.TabSwitchCount
            }).ToListAsync());
    }

    public async Task SaveSnapshotAsync(int studentExamId, string imageBase64)
    {
        var logsDir = Path.Combine(_basePath, "logs");
        Directory.CreateDirectory(logsDir);
        var payload = imageBase64.Contains(',') ? imageBase64[(imageBase64.IndexOf(',') + 1)..] : imageBase64;
        var bytes = Convert.FromBase64String(payload);
        var file = Path.Combine(logsDir, $"snapshot_{studentExamId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.jpg");
        await File.WriteAllBytesAsync(file, bytes);
    }

    private async Task WriteJsonAsync(string relativePath, object data)
    {
        var fullPath = Path.Combine(_basePath, relativePath);
        var parent = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(parent))
            Directory.CreateDirectory(parent);
        var json = JsonSerializer.Serialize(data, JsonOptions);
        await File.WriteAllTextAsync(fullPath, json);
    }
}
