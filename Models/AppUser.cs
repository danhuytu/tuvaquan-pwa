using System.ComponentModel.DataAnnotations;

namespace guithu.Models;

public class AppUser
{
    public int Id { get; set; }
    [Required] public string Username { get; set; } = string.Empty;
    [Required] public string PasswordHash { get; set; } = string.Empty;
    [Required] public string DisplayName { get; set; } = string.Empty;
    public string? AvatarDataUrl { get; set; }
    public string? LoveMailboxImage { get; set; }
    public string? RoastMailboxImage { get; set; }
    public string? BackgroundImage { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsBanned { get; set; }
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
