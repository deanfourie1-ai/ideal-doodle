using BcReleasePlanPortal.Domain;
using Microsoft.EntityFrameworkCore;

namespace BcReleasePlanPortal.Data;

public sealed class BcReleasePlanDbContext(DbContextOptions<BcReleasePlanDbContext> options) : DbContext(options)
{
    public DbSet<RoadmapItem> RoadmapItems => Set<RoadmapItem>();

    public DbSet<ChangeEvent> ChangeEvents => Set<ChangeEvent>();

    public DbSet<ImpactNote> ImpactNotes => Set<ImpactNote>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<CustomerItem> CustomerItems => Set<CustomerItem>();

    public DbSet<ReleasePlan> ReleasePlans => Set<ReleasePlan>();

    public DbSet<ReleasePlanLine> ReleasePlanLines => Set<ReleasePlanLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RoadmapItem>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Modules).HasJsonConversion();
            entity.Property(x => x.ObjectsTouched).HasJsonConversion();

            // The natural key ingest upserts on (design doc §5.1: "external_id ... unique per source").
            entity.HasIndex(x => new { x.Source, x.ExternalId }).IsUnique();

            entity.HasMany(x => x.ChangeEvents)
                .WithOne(x => x.RoadmapItem)
                .HasForeignKey(x => x.RoadmapItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChangeEvent>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.RoadmapItemId, x.DetectedAt });
        });

        modelBuilder.Entity<ImpactNote>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.RoadmapItemId).IsUnique();
            entity.HasOne(x => x.RoadmapItem)
                .WithMany()
                .HasForeignKey(x => x.RoadmapItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Environments).HasJsonConversion();
            entity.Property(x => x.ModulesInUse).HasJsonConversion();
            entity.Property(x => x.AppSourceApps).HasJsonConversion();
            entity.Property(x => x.CustomExtensions).HasJsonConversion();
            entity.Property(x => x.Integrations).HasJsonConversion();
            entity.Property(x => x.Flags).HasJsonConversion();
            entity.Property(x => x.Contacts).HasJsonConversion();
        });

        modelBuilder.Entity<CustomerItem>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.MatchReasons).HasJsonConversion();
            entity.HasIndex(x => new { x.CustomerId, x.RoadmapItemId }).IsUnique();

            entity.HasOne(x => x.Customer)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.RoadmapItem)
                .WithMany()
                .HasForeignKey(x => x.RoadmapItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReleasePlan>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Lines)
                .WithOne(x => x.ReleasePlan)
                .HasForeignKey(x => x.ReleasePlanId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReleasePlanLine>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.RoadmapItemId);
        });
    }
}
