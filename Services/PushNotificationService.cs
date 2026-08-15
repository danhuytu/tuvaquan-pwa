using System.Net;
using System.Text.Json;
using guithu.Data;
using guithu.Models;
using Microsoft.EntityFrameworkCore;
using WebPush;

namespace guithu.Services;

public class PushNotificationService(AppDbContext database, IConfiguration configuration, ILogger<PushNotificationService> logger)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(configuration["Push:VapidPublicKey"])
        && !string.IsNullOrWhiteSpace(configuration["Push:VapidPrivateKey"]);

    public async Task SendLetterNotification(AppUser recipient, AppUser sender, string type)
    {
        if (!IsConfigured) return;

        var senderName = string.IsNullOrWhiteSpace(sender.DisplayName) ? sender.Username : sender.DisplayName;
        var body = type == "roast"
            ? $"{senderName} đang chửi vào mặt mày"
            : $"{senderName} gửi lời yêu thương đến bạn";
        var payload = JsonSerializer.Serialize(new { title = "Tú và Quân", body, icon = "/images/icon-192.png", url = "/" });
        var vapid = new VapidDetails(
            configuration["Push:VapidSubject"] ?? "mailto:contact@tu-va-quan.app",
            configuration["Push:VapidPublicKey"]!,
            configuration["Push:VapidPrivateKey"]!);
        var subscriptions = await database.PushSubscriptions.Where(item => item.UserId == recipient.Id).ToListAsync();
        var expiredIds = new List<int>();

        foreach (var item in subscriptions)
        {
            try
            {
                await new WebPushClient().SendNotificationAsync(new WebPush.PushSubscription(item.Endpoint, item.P256dh, item.Auth), payload, vapid);
            }
            catch (WebPushException exception) when (exception.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
            {
                expiredIds.Add(item.Id);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Không gửi được push notification đến subscription {SubscriptionId}", item.Id);
            }
        }

        if (expiredIds.Count > 0)
        {
            database.PushSubscriptions.RemoveRange(subscriptions.Where(item => expiredIds.Contains(item.Id)));
            await database.SaveChangesAsync();
        }
    }
}
