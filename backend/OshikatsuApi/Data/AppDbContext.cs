using Microsoft.EntityFrameworkCore;
using OshikatsuApi.Entities;

namespace OshikatsuApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Oshi> Oshis => Set<Oshi>();
    public DbSet<NewsItem> NewsItems => Set<NewsItem>();
    public DbSet<ProgramAppearance> ProgramAppearances => Set<ProgramAppearance>();
    public DbSet<RevokedToken> RevokedTokens => Set<RevokedToken>();
    public DbSet<CollectorJobLog> CollectorJobLogs => Set<CollectorJobLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ─── User ───────────────────────────────────────────────────────────
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Email)
                  .HasColumnName("email")
                  .HasMaxLength(254)
                  .IsRequired();
            entity.Property(e => e.PasswordHash)
                  .HasColumnName("password_hash")
                  .IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(e => e.Email)
                  .IsUnique()
                  .HasDatabaseName("ix_users_email");
        });

        // ─── Oshi ────────────────────────────────────────────────────────────
        modelBuilder.Entity<Oshi>(entity =>
        {
            entity.ToTable("oshis");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Name)
                  .HasColumnName("name")
                  .HasMaxLength(100)
                  .IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.User)
                  .WithMany(u => u.Oshis)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.UserId)
                  .HasDatabaseName("ix_oshis_user_id");
        });

        // ─── NewsItem ────────────────────────────────────────────────────────
        modelBuilder.Entity<NewsItem>(entity =>
        {
            entity.ToTable("news_items");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OshiId).HasColumnName("oshi_id");
            entity.Property(e => e.Title).HasColumnName("title").IsRequired();
            entity.Property(e => e.Summary).HasColumnName("summary");
            entity.Property(e => e.SourceUrl).HasColumnName("source_url").IsRequired();
            entity.Property(e => e.PublishedAt).HasColumnName("published_at");
            entity.Property(e => e.IsPublishedAtEstimated)
                  .HasColumnName("is_published_at_estimated");
            entity.Property(e => e.CollectedAt).HasColumnName("collected_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasOne(e => e.Oshi)
                  .WithMany(o => o.NewsItems)
                  .HasForeignKey(e => e.OshiId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.OshiId)
                  .HasDatabaseName("ix_news_items_oshi_id");
            entity.HasIndex(e => e.SourceUrl)
                  .HasDatabaseName("ix_news_items_source_url");
        });

        // ─── ProgramAppearance ───────────────────────────────────────────────
        modelBuilder.Entity<ProgramAppearance>(entity =>
        {
            entity.ToTable("program_appearances");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OshiId).HasColumnName("oshi_id");
            entity.Property(e => e.ProgramName)
                  .HasColumnName("program_name")
                  .IsRequired();
            entity.Property(e => e.Broadcaster).HasColumnName("broadcaster");
            entity.Property(e => e.AiredAt).HasColumnName("aired_at");
            entity.Property(e => e.IsAiredAtEstimated)
                  .HasColumnName("is_aired_at_estimated");
            entity.Property(e => e.SourceUrl).HasColumnName("source_url").IsRequired();
            entity.Property(e => e.CollectedAt).HasColumnName("collected_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasOne(e => e.Oshi)
                  .WithMany(o => o.ProgramAppearances)
                  .HasForeignKey(e => e.OshiId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.OshiId)
                  .HasDatabaseName("ix_program_appearances_oshi_id");
            entity.HasIndex(e => e.SourceUrl)
                  .HasDatabaseName("ix_program_appearances_source_url");
        });

        // ─── CollectorJobLog ──────────────────────────────────────────────────────
        modelBuilder.Entity<CollectorJobLog>(entity =>
        {
            entity.ToTable("collector_job_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.JobType)
                  .HasColumnName("job_type")
                  .HasMaxLength(20)
                  .IsRequired();
            entity.Property(e => e.ExecutedAt).HasColumnName("executed_at");
            entity.Property(e => e.SuccessCount).HasColumnName("success_count");
            entity.Property(e => e.FailureCount).HasColumnName("failure_count");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasIndex(e => new { e.JobType, e.ExecutedAt })
                  .HasDatabaseName("ix_collector_job_logs_job_type_executed_at");
        });

        // ─── RevokedToken ─────────────────────────────────────────────────────
        modelBuilder.Entity<RevokedToken>(entity =>
        {
            entity.ToTable("revoked_tokens");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Jti)
                  .HasColumnName("jti")
                  .HasMaxLength(36)
                  .IsRequired();
            entity.Property(e => e.RevokedAt).HasColumnName("revoked_at");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");

            entity.HasIndex(e => e.Jti)
                  .IsUnique()
                  .HasDatabaseName("ix_revoked_tokens_jti");
        });
    }
}
