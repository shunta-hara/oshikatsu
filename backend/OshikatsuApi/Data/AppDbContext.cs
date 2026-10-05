using Microsoft.EntityFrameworkCore;
using OshikatsuApi.Entities;

namespace OshikatsuApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Oshi> Oshis => Set<Oshi>();
    public DbSet<Talent> Talents => Set<Talent>();
    public DbSet<UserOshi> UserOshis => Set<UserOshi>();
    public DbSet<NewsItem> NewsItems => Set<NewsItem>();
    public DbSet<ProgramAppearance> ProgramAppearances => Set<ProgramAppearance>();
    public DbSet<RevokedToken> RevokedTokens => Set<RevokedToken>();
    public DbSet<CollectorJobLog> CollectorJobLogs => Set<CollectorJobLog>();
    public DbSet<Bookmark> Bookmarks => Set<Bookmark>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();

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
            entity.Property(e => e.LastViewedFeedAt).HasColumnName("last_viewed_feed_at");

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

        // ─── Talent ──────────────────────────────────────────────────────────
        modelBuilder.Entity<Talent>(entity =>
        {
            entity.ToTable("talents");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.DisplayName)
                  .HasColumnName("display_name")
                  .HasMaxLength(100)
                  .IsRequired();
            entity.Property(e => e.NormalizedName)
                  .HasColumnName("normalized_name")
                  .HasMaxLength(100)
                  .IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasIndex(e => e.NormalizedName)
                  .IsUnique()
                  .HasDatabaseName("ix_talents_normalized_name");
        });

        // ─── UserOshi ────────────────────────────────────────────────────────
        modelBuilder.Entity<UserOshi>(entity =>
        {
            entity.ToTable("user_oshis");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.TalentId).HasColumnName("talent_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasOne(e => e.User)
                  .WithMany(u => u.UserOshis)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Talent)
                  .WithMany(t => t.UserOshis)
                  .HasForeignKey(e => e.TalentId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.UserId, e.TalentId })
                  .IsUnique()
                  .HasDatabaseName("ix_user_oshis_user_id_talent_id");

            entity.HasIndex(e => e.UserId)
                  .HasDatabaseName("ix_user_oshis_user_id");

            entity.HasIndex(e => e.TalentId)
                  .HasDatabaseName("ix_user_oshis_talent_id");
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
            entity.Property(e => e.SourceName)
                  .HasColumnName("source_name")
                  .HasMaxLength(200);
            entity.Property(e => e.TalentId).HasColumnName("talent_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasOne(e => e.Oshi)
                  .WithMany(o => o.NewsItems)
                  .HasForeignKey(e => e.OshiId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .IsRequired(false);

            entity.HasOne(e => e.Talent)
                  .WithMany(t => t.NewsItems)
                  .HasForeignKey(e => e.TalentId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .IsRequired(false);

            entity.HasIndex(e => e.OshiId)
                  .HasDatabaseName("ix_news_items_oshi_id");
            entity.HasIndex(e => e.TalentId)
                  .HasDatabaseName("ix_news_items_talent_id");
            entity.HasIndex(e => e.SourceUrl)
                  .HasDatabaseName("ix_news_items_source_url");
            entity.HasIndex(e => e.PublishedAt)
                  .HasDatabaseName("ix_news_items_published_at");
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
            entity.Property(e => e.SourceName)
                  .HasColumnName("source_name")
                  .HasMaxLength(200);
            entity.Property(e => e.TalentId).HasColumnName("talent_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasOne(e => e.Oshi)
                  .WithMany(o => o.ProgramAppearances)
                  .HasForeignKey(e => e.OshiId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .IsRequired(false);

            entity.HasOne(e => e.Talent)
                  .WithMany(t => t.ProgramAppearances)
                  .HasForeignKey(e => e.TalentId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .IsRequired(false);

            entity.HasIndex(e => e.OshiId)
                  .HasDatabaseName("ix_program_appearances_oshi_id");
            entity.HasIndex(e => e.TalentId)
                  .HasDatabaseName("ix_program_appearances_talent_id");
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

        // ─── Bookmark ─────────────────────────────────────────────────────────
        modelBuilder.Entity<Bookmark>(entity =>
        {
            entity.ToTable("bookmarks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.NewsItemId).HasColumnName("news_item_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.NewsItem)
                  .WithMany()
                  .HasForeignKey(e => e.NewsItemId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.UserId, e.NewsItemId })
                  .IsUnique()
                  .HasDatabaseName("ix_bookmarks_user_id_news_item_id");

            entity.HasIndex(e => e.UserId)
                  .HasDatabaseName("ix_bookmarks_user_id");

            entity.HasIndex(e => e.NewsItemId)
                  .HasDatabaseName("ix_bookmarks_news_item_id");
        });

        // ─── PushSubscription ─────────────────────────────────────────────────
        modelBuilder.Entity<PushSubscription>(entity =>
        {
            entity.ToTable("push_subscriptions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Endpoint)
                  .HasColumnName("endpoint")
                  .IsRequired();
            entity.Property(e => e.P256dh)
                  .HasColumnName("p256dh")
                  .IsRequired();
            entity.Property(e => e.Auth)
                  .HasColumnName("auth")
                  .IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.Endpoint)
                  .IsUnique()
                  .HasDatabaseName("ix_push_subscriptions_endpoint");

            entity.HasIndex(e => e.UserId)
                  .HasDatabaseName("ix_push_subscriptions_user_id");
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
