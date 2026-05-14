using System.ComponentModel.DataAnnotations;

namespace CbtExam.Shared.Models;

public class AdminConfig
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string AccessCode { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string Username { get; set; } = "admin";
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? LastLoginAt { get; set; }
    
    [MaxLength(500)]
    public string? Notes { get; set; }
}
