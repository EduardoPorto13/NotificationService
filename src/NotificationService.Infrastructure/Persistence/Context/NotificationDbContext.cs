using Microsoft.EntityFrameworkCore;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence.Context;

public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options)
        : base(options)
    {
    }

    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<NotificationLog>(entity =>
        {
            entity.ToTable("NotificationLogs");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.To)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(x => x.Subject)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(x => x.ErrorMessage);

            entity.Property(x => x.Payload);

            entity.Property(x => x.Attempts)
                .IsRequired();

            entity.Property(x => x.CreatedAt)
                .IsRequired();
        });
    }
}
