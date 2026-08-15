using System.Security.Claims;
using guithu.Data;
using guithu.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;

namespace guithu.Controllers;

[EnableRateLimiting("auth")]
public class AuthController(AppDbContext database) : ApiControllerBase
{
    private static readonly PasswordHasher<AppUser> Passwords = new();

    [HttpPost("register")]
    public async Task<ActionResult<UserDto>> Register(RegisterRequest request)
    {
        var username = NormalizeUsername(request.Username);
        var displayName = request.DisplayName?.Trim();
        if (username.Length < 3 || username.Length > 40 || request.Password?.Length < 4 || string.IsNullOrWhiteSpace(displayName) || displayName.Length > 60)
            return BadRequest(new { message = "Nhập tên hiển thị, username 3–40 ký tự và mật khẩu tối thiểu 4 ký tự." });
        if (await database.Users.AnyAsync(user => user.Username == username))
            return Conflict(new { message = "Username này đã được sử dụng." });

        var user = new AppUser { Username = username, DisplayName = displayName };
        user.PasswordHash = Passwords.HashPassword(user, request.Password!);
        database.Users.Add(user);
        await database.SaveChangesAsync();
        return Ok(ToDto(user));
    }

    [HttpPost("login")]
    public async Task<ActionResult<UserDto>> Login(LoginRequest request)
    {
        var username = NormalizeUsername(request.Username);
        var user = await database.Users.SingleOrDefaultAsync(item => item.Username == username);
        if (user is null || Passwords.VerifyHashedPassword(user, user.PasswordHash, request.Password ?? string.Empty) == PasswordVerificationResult.Failed)
            return Unauthorized(new { message = "Username hoặc mật khẩu chưa đúng." });
        if (user.IsBanned) return StatusCode(StatusCodes.Status403Forbidden, new { message = "Tài khoản của bạn đã bị tạm khóa." });
        if (!string.Equals(request.LoveAnswer?.Trim(), "có", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Câu trả lời phải là “Có” nhé." });

        user.LastActiveAt = DateTime.UtcNow;
        await database.SaveChangesAsync();
        await SignIn(user);
        return Ok(ToDto(user));
    }

    [Authorize, HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [Authorize, HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me()
    {
        var user = await database.Users.FindAsync(CurrentUserId);
        return user is null ? Unauthorized() : Ok(ToDto(user));
    }

    [Authorize, HttpPut("profile")]
    public async Task<ActionResult<UserDto>> UpdateProfile(ProfileRequest request)
    {
        var user = await database.Users.FindAsync(CurrentUserId);
        if (user is null) return Unauthorized();
        var name = request.DisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 60) return BadRequest(new { message = "Tên hiển thị không hợp lệ." });
        if (request.AvatarDataUrl?.Length > 2_000_000 || request.LoveMailboxImage?.Length > 2_000_000 || request.RoastMailboxImage?.Length > 2_000_000 || request.BackgroundImage?.Length > 2_000_000) return BadRequest(new { message = "Mỗi ảnh tối đa 1.5 MB." });
        if (!IsSafeImageOrEmpty(request.AvatarDataUrl) || !IsSafeImageOrEmpty(request.LoveMailboxImage) || !IsSafeImageOrEmpty(request.RoastMailboxImage) || !IsSafeImageOrEmpty(request.BackgroundImage)) return BadRequest(new { message = "Chỉ chấp nhận ảnh PNG, JPG hoặc WebP." });
        user.DisplayName = name;
        if (request.AvatarDataUrl is not null) user.AvatarDataUrl = string.IsNullOrEmpty(request.AvatarDataUrl) ? null : request.AvatarDataUrl;
        if (request.LoveMailboxImage is not null) user.LoveMailboxImage = string.IsNullOrEmpty(request.LoveMailboxImage) ? null : request.LoveMailboxImage;
        if (request.RoastMailboxImage is not null) user.RoastMailboxImage = string.IsNullOrEmpty(request.RoastMailboxImage) ? null : request.RoastMailboxImage;
        if (request.BackgroundImage is not null) user.BackgroundImage = string.IsNullOrEmpty(request.BackgroundImage) ? null : request.BackgroundImage;
        await database.SaveChangesAsync();
        return Ok(ToDto(user));
    }

    private async Task SignIn(AppUser user) => await HttpContext.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Name, user.Username), ..(user.IsAdmin ? new[] { new Claim(ClaimTypes.Role, "Admin") } : [])], CookieAuthenticationDefaults.AuthenticationScheme)),
        new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14) });
    private static string NormalizeUsername(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;
    private static UserDto ToDto(AppUser user) => new(user.Username, user.DisplayName, user.AvatarDataUrl, user.LoveMailboxImage, user.RoastMailboxImage, user.BackgroundImage, user.IsAdmin);
    private static bool IsSafeImageOrEmpty(string? value) => string.IsNullOrEmpty(value) || IsSafeAvatar(value);
    private static bool IsSafeAvatar(string value) => value.StartsWith("data:image/png;base64,", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("data:image/jpeg;base64,", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("data:image/webp;base64,", StringComparison.OrdinalIgnoreCase);
}

public record RegisterRequest(string? Username, string? Password, string? DisplayName);
public record LoginRequest(string? Username, string? Password, string? LoveAnswer);
public record ProfileRequest(string? DisplayName, string? AvatarDataUrl, string? LoveMailboxImage, string? RoastMailboxImage, string? BackgroundImage);
public record UserDto(string Username, string DisplayName, string? AvatarDataUrl, string? LoveMailboxImage, string? RoastMailboxImage, string? BackgroundImage, bool IsAdmin);
