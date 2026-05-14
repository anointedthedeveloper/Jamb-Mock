using CbtExam.Data;
using CbtExam.Shared.DTOs;
using CbtExam.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CbtExam.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StudentsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await db.Students
            .OrderBy(s => s.FullName)
            .Select(s => new StudentAdminDto(s.Id, s.FullName, s.StudentId, s.IsActive))
            .ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Upsert(StudentUpsertDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName))
            return BadRequest("Full name is required.");

        Student? entity = null;
        if (dto.Id.HasValue)
            entity = await db.Students.FindAsync(dto.Id.Value);

        // Auto-generate StudentId if not provided
        var studentId = dto.StudentId?.Trim();
        if (string.IsNullOrWhiteSpace(studentId))
        {
            var count = await db.Students.CountAsync();
            studentId = $"STU-{(count + 1):D4}";
        }

        // Ensure uniqueness on new records
        if (entity is null && await db.Students.AnyAsync(s => s.StudentId == studentId))
            studentId = $"STU-{(await db.Students.CountAsync() + 1):D4}-{Guid.NewGuid().ToString()[..4]}";

        if (entity is null)
        {
            entity = new Student
            {
                FullName  = dto.FullName.Trim(),
                StudentId = studentId,
                IsActive  = dto.IsActive,
                Password  = string.IsNullOrWhiteSpace(dto.Password) ? "1234" : dto.Password.Trim()
            };
            db.Students.Add(entity);
        }
        else
        {
            entity.FullName  = dto.FullName.Trim();
            entity.StudentId = studentId;
            entity.IsActive  = dto.IsActive;
            if (!string.IsNullOrWhiteSpace(dto.Password))
                entity.Password = dto.Password.Trim();
        }

        await db.SaveChangesAsync();
        return Ok(new StudentAdminDto(entity.Id, entity.FullName, entity.StudentId, entity.IsActive));
    }

    [HttpPost("password")]
    public async Task<IActionResult> UpdatePassword(StudentPasswordUpdateDto dto)
    {
        var student = await db.Students.FindAsync(dto.StudentId);
        if (student is null) return NotFound();
        if (string.IsNullOrWhiteSpace(dto.NewPassword)) return BadRequest("Password cannot be empty.");
        student.Password = dto.NewPassword.Trim();
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var student = await db.Students.FindAsync(id);
        if (student is null) return NotFound();
        db.Students.Remove(student);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
