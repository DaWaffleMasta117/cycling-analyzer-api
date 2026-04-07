using Microsoft.EntityFrameworkCore;
using CyclingAnalyzer.Api.Models.Entities;

namespace CyclingAnalyzer.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Athlete> Athletes => Set<Athlete>();
    public DbSet<AthleteToken> AthleteTokens => Set<AthleteToken>();
    public DbSet<Ride> Rides => Set<Ride>();
    public DbSet<RidePowerStream> RidePowerStreams => Set<RidePowerStream>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Athlete>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(a => a.LastName).IsRequired().HasMaxLength(100);
        });

        modelBuilder.Entity<AthleteToken>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.HasOne(t => t.Athlete)
                  .WithOne(a => a.Token)
                  .HasForeignKey<AthleteToken>(t => t.AthleteId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.Property(t => t.AccessToken).IsRequired();
            entity.Property(t => t.RefreshToken).IsRequired();
        });

        modelBuilder.Entity<Ride>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.HasOne(r => r.Athlete)
                  .WithMany()
                  .HasForeignKey(r => r.AthleteId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Index on AthleteId + StartDate — the most common query pattern
            entity.HasIndex(r => new { r.AthleteId, r.StartDate });
        });

        modelBuilder.Entity<RidePowerStream>(entity =>
        {
            // RideId is both the PK and the FK — strict 1-to-1 with Ride
            entity.HasKey(s => s.RideId);

            entity.HasOne(s => s.Ride)
                  .WithOne()
                  .HasForeignKey<RidePowerStream>(s => s.RideId)
                  .OnDelete(DeleteBehavior.Cascade);

            // WattsJson can be large (hours of second-by-second data)
            entity.Property(s => s.WattsJson).IsRequired();
        });
    }
}