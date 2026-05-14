using CbtExam.Data;
using CbtExam.Shared.DTOs;
using CbtExam.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CbtExam.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QuestionBankController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? subject, [FromQuery] int? year)
    {
        var q = db.QuestionBank.AsQueryable();
        if (!string.IsNullOrWhiteSpace(subject)) q = q.Where(x => x.Subject == subject);
        if (year.HasValue) q = q.Where(x => x.Year == year.Value);
        return Ok(await q.OrderBy(x => x.Subject).ThenBy(x => x.Year).ThenBy(x => x.QuestionNumber)
            .Select(x => new QuestionBankDto(x.Id, x.Subject, x.Year, x.QuestionNumber, x.Text, x.OptionsJson, x.CorrectAnswer))
            .ToListAsync());
    }

    [HttpGet("subjects")]
    public async Task<IActionResult> GetSubjects() =>
        Ok(await db.QuestionBank.Select(q => q.Subject).Distinct().OrderBy(s => s).ToListAsync());

    [HttpGet("years")]
    public async Task<IActionResult> GetYears([FromQuery] string subject) =>
        Ok(await db.QuestionBank.Where(q => q.Subject == subject).Select(q => q.Year).Distinct().OrderBy(y => y).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Add(QuestionBankCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Text)) return BadRequest("Question text required.");
        if (dto.Options is null || dto.Options.Count < 2) return BadRequest("At least 2 options required.");
        if (!dto.Options.Contains(dto.CorrectAnswer, StringComparer.OrdinalIgnoreCase))
            return BadRequest("Correct answer must match one of the options.");

        var q = new QuestionBank
        {
            Subject = dto.Subject.Trim(),
            Year = dto.Year,
            QuestionNumber = dto.QuestionNumber,
            Text = dto.Text.Trim(),
            OptionsJson = JsonSerializer.Serialize(dto.Options),
            CorrectAnswer = dto.CorrectAnswer
        };
        db.QuestionBank.Add(q);
        await db.SaveChangesAsync();
        return Ok(q.Id);
    }

    [HttpPost("import")]
    public async Task<IActionResult> Import(List<QuestionBankCreateDto> questions)
    {
        if (questions.Count == 0) return BadRequest("No questions provided.");
        var valid = new List<QuestionBank>();
        foreach (var dto in questions)
        {
            if (string.IsNullOrWhiteSpace(dto.Text) || dto.Options is null || dto.Options.Count < 2) continue;
            if (!dto.Options.Contains(dto.CorrectAnswer, StringComparer.OrdinalIgnoreCase)) continue;
            valid.Add(new QuestionBank
            {
                Subject = dto.Subject.Trim(),
                Year = dto.Year,
                QuestionNumber = dto.QuestionNumber <= 0 ? valid.Count + 1 : dto.QuestionNumber,
                Text = dto.Text.Trim(),
                OptionsJson = JsonSerializer.Serialize(dto.Options),
                CorrectAnswer = dto.CorrectAnswer
            });
        }
        db.QuestionBank.AddRange(valid);
        await db.SaveChangesAsync();
        return Ok(new { imported = valid.Count, skipped = questions.Count - valid.Count });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, QuestionBankCreateDto dto)
    {
        var q = await db.QuestionBank.FindAsync(id);
        if (q is null) return NotFound();
        q.Subject = dto.Subject.Trim();
        q.Year = dto.Year;
        q.QuestionNumber = dto.QuestionNumber;
        q.Text = dto.Text.Trim();
        q.OptionsJson = JsonSerializer.Serialize(dto.Options);
        q.CorrectAnswer = dto.CorrectAnswer;
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var q = await db.QuestionBank.FindAsync(id);
        if (q is null) return NotFound();
        db.QuestionBank.Remove(q);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // Generate an exam by randomly picking questions from the bank per subject/year config
    [HttpPost("generate-exam")]
    public async Task<IActionResult> GenerateExam(ExamGenerateDto dto)
    {
        if (dto.Subjects.Count == 0) return BadRequest("At least one subject required.");
        if (dto.Subjects.Count > 4) return BadRequest("Maximum 4 subjects allowed.");

        var exam = new Exam
        {
            Title = dto.Title,
            Subject = string.Join(", ", dto.Subjects.Select(s => s.Subject)),
            DurationMinutes = dto.DurationMinutes,
            ShuffleQuestions = dto.ShuffleQuestions,
            ShuffleOptions = dto.ShuffleOptions,
            CreatedAt = DateTime.UtcNow
        };
        db.Exams.Add(exam);
        await db.SaveChangesAsync();

        var rng = new Random();
        int qNum = 1;
        foreach (var subjectConfig in dto.Subjects)
        {
            var pool = await db.QuestionBank
                .Where(q => q.Subject == subjectConfig.Subject && subjectConfig.Years.Contains(q.Year))
                .ToListAsync();

            var picked = pool.OrderBy(_ => rng.Next()).Take(subjectConfig.QuestionCount);
            foreach (var bq in picked)
            {
                db.Questions.Add(new Question
                {
                    ExamId = exam.Id,
                    QuestionNumber = qNum++,
                    Text = bq.Text,
                    OptionsJson = bq.OptionsJson,
                    CorrectAnswer = bq.CorrectAnswer
                });
            }
        }
        await db.SaveChangesAsync();
        return Ok(exam.Id);
    }
}
