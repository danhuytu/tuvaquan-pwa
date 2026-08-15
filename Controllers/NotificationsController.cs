using guithu.Data;
using guithu.Models;
using guithu.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace guithu.Controllers;

[Authorize]
public class NotificationsController(AppDbContext database, PushNotificationService push) : ApiControllerBase
{
    [HttpGet("vapid-public-key")]
    public IActionResult VapidPublicKey()
        => push.IsConfigured ? Ok(new { publicKey = HttpContext.RequestServices.GetRequiredService<IConfiguration>()["Push:VapidPublicKey"] }) : Problem("Push notification chưa được cấu hình trên server.", statusCode: 503);

    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe(PushSubscriptionRequest request)
    {
        if (!push.IsConfigured) return Problem("Push notification chưa được cấu hình trên server.", statusCode: 503);
        if (!Uri.TryCreate(request.Endpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme != Uri.UriSchemeHttps ||
            request.Keys is null || string.IsNullOrWhiteSpace(request.Keys.P256dh) || string.IsNullOrWhiteSpace(request.Keys.Auth) ||
            request.Endpoint.Length > 2048 || request.Keys.P256dh.Length > 200 || request.Keys.Auth.Length > 200)
            return BadRequest(new { message = "Dữ liệu đăng ký thông báo không hợp lệ." });

        var subscription = await database.PushSubscriptions.SingleOrDefaultAsync(item => item.Endpoint == request.Endpoint);
        if (subscription is null)
        {
            database.PushSubscriptions.Add(new PushSubscription { UserId = CurrentUserId, Endpoint = request.Endpoint, P256dh = request.Keys.P256dh, Auth = request.Keys.Auth });
        }
        else
        {
            subscription.UserId = CurrentUserId;
            subscription.P256dh = request.Keys.P256dh;
            subscription.Auth = request.Keys.Auth;
            subscription.UpdatedAt = DateTime.UtcNow;
        }
        await database.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("unsubscribe")]
    public async Task<IActionResult> Unsubscribe(UnsubscribeRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Endpoint))
        {
            var subscription = await database.PushSubscriptions.SingleOrDefaultAsync(item => item.UserId == CurrentUserId && item.Endpoint == request.Endpoint);
            if (subscription is not null) database.PushSubscriptions.Remove(subscription);
            await database.SaveChangesAsync();
        }
        return NoContent();
    }
}

public record PushSubscriptionRequest(string? Endpoint, PushKeys? Keys);
public record PushKeys(string? P256dh, string? Auth);
public record UnsubscribeRequest(string? Endpoint);
