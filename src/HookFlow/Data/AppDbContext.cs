using Microsoft.EntityFrameworkCore;
using HookFlow.Models;

namespace HookFlow.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<WebhookSubscription> Subscriptions => Set<WebhookSubscription>();
    public DbSet<WebhookDeliveryAttempt> DeliveryAttempts => Set<WebhookDeliveryAttempt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<WebhookSubscription>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(150).IsRequired();
            entity.Property(e => e.TargetUrl).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.SecretKey).HasMaxLength(256).IsRequired();
            entity.Property(e => e.EventType).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => e.EventType);
        });

        modelBuilder.Entity<WebhookDeliveryAttempt>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EventType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Payload).IsRequired();
            entity.HasIndex(e => new { e.Status, e.ScheduledAtUtc });

            entity.HasOne(e => e.Subscription)
                .WithMany(s => s.DeliveryAttempts)
                .HasForeignKey(e => e.SubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
