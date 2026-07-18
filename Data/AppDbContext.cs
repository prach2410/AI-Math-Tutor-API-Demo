using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<LearningSessionEntity> LearningSessions => Set<LearningSessionEntity>();
    public DbSet<DiscoveryBatchEntity> DiscoveryBatches => Set<DiscoveryBatchEntity>();
    public DbSet<ProjectBrainEvidenceEntity> ProjectBrainEvidence => Set<ProjectBrainEvidenceEntity>();
    public DbSet<HomeworkReadEntity> HomeworkReads => Set<HomeworkReadEntity>();
    public DbSet<TeachingSessionEntity> TeachingSessions => Set<TeachingSessionEntity>();
    public DbSet<RecallEventEntity> RecallEvents => Set<RecallEventEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LearningSessionEntity>().HasKey(e => e.SessionId);
        modelBuilder.Entity<DiscoveryBatchEntity>().HasKey(e => e.BatchId);
        modelBuilder.Entity<ProjectBrainEvidenceEntity>().HasKey(e => e.Id);
        modelBuilder.Entity<HomeworkReadEntity>().HasKey(e => e.Id);
        modelBuilder.Entity<HomeworkReadEntity>().Property(e => e.Id).ValueGeneratedOnAdd();
        modelBuilder.Entity<TeachingSessionEntity>().HasKey(e => e.Id);
        modelBuilder.Entity<RecallEventEntity>().HasKey(e => e.Id);
        modelBuilder.Entity<RecallEventEntity>().Property(e => e.Id).ValueGeneratedOnAdd();
    }
}
