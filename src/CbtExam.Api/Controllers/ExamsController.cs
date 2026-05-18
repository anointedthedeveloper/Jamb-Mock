using CbtExam.Data;
using CbtExam.Shared.DTOs;
using CbtExam.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CbtExam.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExamsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = false)
    {
        IQueryable<Exam> query = db.Exams;
        if (activeOnly)
        {
            var activeExamIds = await db.ExamSessions
                .Where(s => s.IsActive)
                .Select(s => s.ExamId)
                .Distinct()
                .ToListAsync();
            query = query.Where(e => activeExamIds.Contains(e.Id));
        }

        return Ok(await query
            .Select(e => new ExamDto(e.Id, e.Title, e.Subject, e.DurationMinutes, e.ShuffleQuestions, e.ShuffleOptions, e.AccessPassword, e.CreatedAt, e.Questions.Count))
            .ToListAsync());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var exam = await db.Exams.Include(e => e.Questions).FirstOrDefaultAsync(e => e.Id == id);
        return exam is null ? NotFound() : Ok(exam);
    }

    [HttpPost]
    public async Task<IActionResult> Create(ExamCreateDto dto)
    {
        var exam = new Exam
        {
            Title = dto.Title, Subject = dto.Subject,
            DurationMinutes = dto.DurationMinutes,
            ShuffleQuestions = dto.ShuffleQuestions,
            ShuffleOptions = dto.ShuffleOptions,
            AccessPassword = dto.AccessPassword ?? string.Empty
        };
        db.Exams.Add(exam);
        await db.SaveChangesAsync();
        return Ok(exam.Id);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, ExamCreateDto dto)
    {
        var exam = await db.Exams.FindAsync(id);
        if (exam is null) return NotFound();
        exam.Title = dto.Title;
        exam.Subject = dto.Subject;
        exam.DurationMinutes = dto.DurationMinutes;
        exam.ShuffleQuestions = dto.ShuffleQuestions;
        exam.ShuffleOptions = dto.ShuffleOptions;
        exam.AccessPassword = dto.AccessPassword ?? string.Empty;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var exam = await db.Exams.FindAsync(id);
        if (exam is null) return NotFound();
        db.Exams.Remove(exam);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id}/questions")]
    public async Task<IActionResult> AddQuestion(int id, QuestionCreateDto dto)
    {
        var exam = await db.Exams.FindAsync(id);
        if (exam is null) return NotFound();
        if (string.IsNullOrWhiteSpace(dto.Text)) return BadRequest("Question text is required.");
        if (dto.Options is null || dto.Options.Count < 2) return BadRequest("At least two options are required.");
        if (dto.Options.Any(o => string.IsNullOrWhiteSpace(o))) return BadRequest("Options cannot be blank.");
        
        var resolvedAnswer = ResolveCorrectAnswer(dto.CorrectAnswer, dto.Options);
        if (!dto.Options.Contains(resolvedAnswer, StringComparer.OrdinalIgnoreCase))
            return BadRequest("Correct answer must match one of the options.");
        var q = new Question
        {
            ExamId = id,
            QuestionNumber = dto.QuestionNumber,
            Text = dto.Text,
            OptionsJson = JsonSerializer.Serialize(dto.Options),
            CorrectAnswer = resolvedAnswer,
            Subject = dto.Subject ?? string.Empty,
            Year = dto.Year
        };
        db.Questions.Add(q);
        await db.SaveChangesAsync();
        return Ok(q.Id);
    }

    [HttpPost("{id}/questions/import")]
    public async Task<IActionResult> ImportQuestions(int id, List<QuestionCreateDto> questions)
    {
        var exam = await db.Exams.FindAsync(id);
        if (exam is null) return NotFound();
        if (questions.Count == 0) return BadRequest("No questions provided.");

        var valid = new List<Question>();
        foreach (var dto in questions)
        {
            if (string.IsNullOrWhiteSpace(dto.Text)) continue;
            if (dto.Options is null || dto.Options.Count < 2) continue;
            var resolvedAnswer = ResolveCorrectAnswer(dto.CorrectAnswer, dto.Options);
            if (!dto.Options.Contains(resolvedAnswer, StringComparer.OrdinalIgnoreCase)) continue;
            valid.Add(new Question
            {
                ExamId = id,
                QuestionNumber = dto.QuestionNumber <= 0 ? valid.Count + 1 : dto.QuestionNumber,
                Text = dto.Text,
                OptionsJson = JsonSerializer.Serialize(dto.Options),
                CorrectAnswer = resolvedAnswer,
                Subject = dto.Subject ?? string.Empty,
                Year = dto.Year
            });
        }

        db.Questions.AddRange(valid);
        await db.SaveChangesAsync();
        return Ok(new { imported = valid.Count, skipped = questions.Count - valid.Count });
    }

    [HttpGet("{id}/questions")]
    public async Task<IActionResult> GetQuestions(int id) =>
        Ok(await db.Questions.Where(q => q.ExamId == id).OrderBy(q => q.QuestionNumber).ToListAsync());

    [HttpDelete("questions/{questionId}")]
    public async Task<IActionResult> DeleteQuestion(int questionId)
    {
        var q = await db.Questions.FindAsync(questionId);
        if (q is null) return NotFound();
        db.Questions.Remove(q);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static string ResolveCorrectAnswer(string correctAnswer, List<string> options)
    {
        if (options is null || options.Count == 0 || string.IsNullOrWhiteSpace(correctAnswer)) return correctAnswer;
        
        var clean = correctAnswer.Trim().ToUpper();
        if (clean == "A" && options.Count >= 1) return options[0];
        if (clean == "B" && options.Count >= 2) return options[1];
        if (clean == "C" && options.Count >= 3) return options[2];
        if (clean == "D" && options.Count >= 4) return options[3];
        
        return correctAnswer;
    }
}
