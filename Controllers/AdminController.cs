using guithu.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace guithu.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController(AppDbContext database) : ApiControllerBase
{
    [HttpGet("users")]
    public async Task<ActionResult<IEnumerable<AdminUserDto>>> Users() => Ok(await database.Users
        .OrderByDescending(user => user.CreatedAt)
        .Select(user => new AdminUserDto(user.Id, user.Username, user.DisplayName, user.CreatedAt, user.LastActiveAt, user.IsBanned, user.IsAdmin))
        .ToListAsync());

    [HttpPut("users/{id:int}/ban")]
    public async Task<ActionResult<AdminUserDto>> SetBanStatus(int id, BanRequest request)
    {
        var user = await database.Users.FindAsync(id);
        if (user is null) return NotFound();
        if (user.Id == CurrentUserId) return BadRequest(new { message = "Bạn không thể tự khóa tài khoản admin." });
        user.IsBanned = request.IsBanned;
        await database.SaveChangesAsync();
        return Ok(new AdminUserDto(user.Id, user.Username, user.DisplayName, user.CreatedAt, user.LastActiveAt, user.IsBanned, user.IsAdmin));
    }
}

public record BanRequest(bool IsBanned);
public record AdminUserDto(int Id, string Username, string DisplayName, DateTime CreatedAt, DateTime LastActiveAt, bool IsBanned, bool IsAdmin);
