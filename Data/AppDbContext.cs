using guithu.Models;
using Microsoft.EntityFrameworkCore;

namespace guithu.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Letter> Letters => Set<Letter>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasIndex(user => user.Username).IsUnique();
            entity.Property(user => user.Username).HasMaxLength(40);
            entity.Property(user => user.DisplayName).HasMaxLength(60);
        });
        modelBuilder.Entity<Letter>(entity =>
        {
            entity.Property(letter => letter.Content).HasMaxLength(1000);
            entity.HasOne(letter => letter.Sender).WithMany().HasForeignKey(letter => letter.SenderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(letter => letter.Recipient).WithMany().HasForeignKey(letter => letter.RecipientId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<PushSubscription>(entity =>
        {
            entity.HasIndex(subscription => subscription.Endpoint).IsUnique();
            entity.Property(subscription => subscription.Endpoint).HasMaxLength(2048);
            entity.Property(subscription => subscription.P256dh).HasMaxLength(200);
            entity.Property(subscription => subscription.Auth).HasMaxLength(200);
            entity.HasOne(subscription => subscription.User).WithMany().HasForeignKey(subscription => subscription.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
