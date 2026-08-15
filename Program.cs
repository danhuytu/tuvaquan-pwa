using guithu.Data;
using guithu.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Claims;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

var postgresConnection = builder.Configuration.GetConnectionString("Postgres");
if (string.IsNullOrWhiteSpace(postgresConnection))
    throw new InvalidOperationException("Ứng dụng cần ConnectionStrings__Postgres để kết nối PostgreSQL.");
postgresConnection = PostgresConnection.Normalize(postgresConnection);

builder.Services.AddControllersWithViews(options => options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(postgresConnection));
builder.Services.AddScoped<PushNotificationService>();
var dataProtectionPath = builder.Configuration["DataProtection:KeysPath"];
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("TuVaQuan");
if (!string.IsNullOrWhiteSpace(dataProtectionPath))
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath));
else
    dataProtection.PersistKeysToDbContext<AppDbContext>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/";
        options.Cookie.Name = "__Host-TuVaQuan";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    });
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 8, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    options.AddPolicy("api", context => RateLimitPartition.GetSlidingWindowLimiter(
        context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
        _ => new SlidingWindowRateLimiterOptions { PermitLimit = 100, Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 4, QueueLimit = 0, AutoReplenishment = true }));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.Use(async (context, next) =>
{
    context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; script-src 'self'; style-src 'self' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com; img-src 'self' data:; connect-src 'self'; object-src 'none'; base-uri 'self'; frame-ancestors 'none'; form-action 'self'";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

app.UseRateLimiter();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api") && int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
    {
        var user = await context.RequestServices.GetRequiredService<AppDbContext>().Users.FindAsync(userId);
        if (user?.IsBanned == true)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { message = "Tài khoản của bạn đã bị tạm khóa." });
            return;
        }
        if (user is not null && DateTime.UtcNow - user.LastActiveAt > TimeSpan.FromMinutes(1))
        {
            user.LastActiveAt = DateTime.UtcNow;
            await context.RequestServices.GetRequiredService<AppDbContext>().SaveChangesAsync();
        }
    }
    await next();
});
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllers().RequireRateLimiting("api");
app.MapGet("/health", async (AppDbContext database) =>
    await database.Database.CanConnectAsync() ? Results.Ok(new { status = "ok" }) : Results.Problem(statusCode: 503));

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    database.Database.Migrate();

    // Tài khoản quản trị ban đầu dùng chung cho SQLite local và PostgreSQL production.
    var admin = database.Users.SingleOrDefault(user => user.Username == "huytu");
    if (admin is null)
    {
        database.Users.Add(new guithu.Models.AppUser
        {
            Username = "huytu",
            DisplayName = "huytu",
            PasswordHash = "AQAAAAIAAYagAAAAEFR2YX92iXqYQJeN86GH93sBfi7l0bVeutbx1do18MF0Dj7GUdq4ijM9oRbaYXn9Eg==",
            IsAdmin = true
        });
    }
    else
    {
        admin.IsAdmin = true;
    }
    database.SaveChanges();
}


app.Run();
