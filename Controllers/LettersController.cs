using guithu.Data;
using guithu.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace guithu.Controllers;

[Authorize]
public class LettersController(AppDbContext database, guithu.Services.PushNotificationService push) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<LetterDto>>> List(string folder = "received", string? type = null, bool unreadOnly = false)
    {
        var isSent = string.Equals(folder, "sent", StringComparison.OrdinalIgnoreCase);
        IQueryable<Letter> query = database.Letters.Include(letter => letter.Sender).Include(letter => letter.Recipient)
            .Where(letter => isSent ? letter.SenderId == CurrentUserId : letter.RecipientId == CurrentUserId);
        if (type is "love" or "roast") query = query.Where(letter => letter.Type == type);
        if (unreadOnly) query = query.Where(letter => !letter.IsRead);
        return Ok(await query.OrderByDescending(letter => letter.CreatedAt).Select(letter => ToDto(letter)).ToListAsync());
    }

    [HttpPost]
    public async Task<ActionResult<LetterDto>> Send(SendLetterRequest request)
    {
        var username = request.Recipient?.Trim().ToLowerInvariant();
        var recipient = await database.Users.SingleOrDefaultAsync(user => user.Username == username);
        if (recipient is null) return NotFound(new { message = "Chưa tìm thấy username người nhận." });
        if (request.Type is not ("love" or "roast") || string.IsNullOrWhiteSpace(request.Content) || request.Content.Trim().Length > 1000)
            return BadRequest(new { message = "Nội dung hoặc loại thư không hợp lệ." });
        var letter = new Letter { SenderId = CurrentUserId, RecipientId = recipient.Id, Type = request.Type, Content = request.Content.Trim() };
        database.Letters.Add(letter);
        await database.SaveChangesAsync();
        await database.Entry(letter).Reference(item => item.Sender).LoadAsync();
        await database.Entry(letter).Reference(item => item.Recipient).LoadAsync();
        await push.SendLetterNotification(letter.Recipient, letter.Sender, letter.Type);
        return Ok(ToDto(letter));
    }

    [HttpPost("mark-read")]
    public async Task<IActionResult> MarkRead(MarkReadRequest request)
    {
        var ids = request.Ids?.Distinct().ToArray() ?? [];
        var letters = await database.Letters.Where(letter => letter.RecipientId == CurrentUserId && ids.Contains(letter.Id)).ToListAsync();
        letters.ForEach(letter =>
        {
            letter.IsRead = true;
            letter.ReadAt ??= DateTime.UtcNow;
        });
        await database.SaveChangesAsync();
        return NoContent();
    }

    private static LetterDto ToDto(Letter letter) => new(letter.Id, letter.Sender.Username, letter.Sender.DisplayName, letter.Sender.AvatarDataUrl, letter.Recipient.Username, letter.Recipient.DisplayName, letter.Recipient.AvatarDataUrl, letter.Type, letter.Content, letter.CreatedAt, letter.IsRead, letter.ReadAt);
}

public record SendLetterRequest(string? Recipient, string? Type, string? Content);
public record MarkReadRequest(IEnumerable<int>? Ids);
public record LetterDto(int Id, string From, string FromDisplayName, string? FromAvatarDataUrl, string To, string ToDisplayName, string? ToAvatarDataUrl, string Type, string Content, DateTime CreatedAt, bool IsRead, DateTime? ReadAt);
