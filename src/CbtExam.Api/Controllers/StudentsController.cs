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
        if (string.IsNullOrWhiteSpace(dto.FullName) || string.IsNullOrWhiteSpace(dto.StudentId))
            return BadRequest("Full name and student ID are required.");

        Student? entity = null;
        if (dto.Id.HasValue)
            entity = await db.Students.FindAsync(dto.Id.Value);

        if (entity is null)
        {
            entity = new Student
            {
                FullName = dto.FullName.Trim(),
                StudentId = dto.StudentId.Trim(),
                IsActive = dto.IsActive
            };
            db.Students.Add(entity);
        }
        else
        {
            entity.FullName = dto.FullName.Trim();
            entity.StudentId = dto.StudentId.Trim();
            entity.IsActive = dto.IsActive;
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
