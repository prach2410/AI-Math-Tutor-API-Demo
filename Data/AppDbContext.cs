using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<LearningSessionEntity> LearningSessions => Set<LearningSessionEntity>();
    public DbSet<DiscoveryBatchEntity> DiscoveryBatches => Set<DiscoveryBatchEntity>();
    public DbSet<ProjectBrainEvidenceEntity> ProjectBrainEvidence => Set<ProjectBrainEvidenceEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LearningSessionEntity>().HasKey(e => e.SessionId);
        modelBuilder.Entity<DiscoveryBatchEntity>().HasKey(e => e.BatchId);
        modelBuilder.Entity<ProjectBrainEvidenceEntity>().HasKey(e => e.Id);
    }
}
