using BackEnd.Models;
using Microsoft.EntityFrameworkCore;

namespace BackEnd.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppointmentRequest> AppointmentRequests => Set<AppointmentRequest>();
    public DbSet<Review> Reviews => Set<Review>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppointmentRequest>().HasIndex(item => item.Reference).IsUnique();
        modelBuilder.Entity<AppointmentRequest>().Property(item => item.Reference).HasMaxLength(16);
        modelBuilder.Entity<AppointmentRequest>().Property(item => item.PreferredLanguage).HasMaxLength(5);
        modelBuilder.Entity<AppointmentRequest>().Property(item => item.ServiceId).HasMaxLength(64);
        modelBuilder.Entity<AppointmentRequest>().Property(item => item.Status).HasMaxLength(32);
        modelBuilder.Entity<Review>().Property(item => item.Name).HasMaxLength(120);
        modelBuilder.Entity<Review>().Property(item => item.ServiceName).HasMaxLength(120);
    }
}