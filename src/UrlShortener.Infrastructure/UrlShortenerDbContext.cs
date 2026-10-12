namespace UrlShortener.Infrastructure;

using Microsoft.EntityFrameworkCore;
using UrlShortener.Domain;

/// <summary>
/// EF Core DbContext for the URL Shortener.
/// </summary>
public class UrlShortenerDbContext : DbContext
{
    public DbSet<ShortenedUrl> ShortenedUrls { get; set; } = null!;
    public DbSet<ClickEvent> ClickEvents { get; set; } = null!;

    public UrlShortenerDbContext(DbContextOptions<UrlShortenerDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure ShortenedUrl
        var shortenedUrlBuilder = modelBuilder.Entity<ShortenedUrl>();

        shortenedUrlBuilder.HasKey(x => x.Id);
        shortenedUrlBuilder.Property(x => x.ShortCode)
            .IsRequired()
            .HasMaxLength(8);
        shortenedUrlBuilder.Property(x => x.OriginalUrl)
            .IsRequired()
            .HasMaxLength(2048);
        shortenedUrlBuilder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        // Unique index on ShortCode for fast lookups and duplicate prevention
        shortenedUrlBuilder.HasIndex(x => x.ShortCode)
            .IsUnique()
            .HasDatabaseName("IX_ShortCode");

        // Configure ClickEvent
        var clickEventBuilder = modelBuilder.Entity<ClickEvent>();

        clickEventBuilder.HasKey(x => x.Id);
        clickEventBuilder.Property(x => x.ShortenedUrlId)
            .IsRequired();
        clickEventBuilder.Property(x => x.ClickedAtUtc)
            .IsRequired();

        // Foreign key reference
        clickEventBuilder.HasOne<ShortenedUrl>()
            .WithMany()
            .HasForeignKey(x => x.ShortenedUrlId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_ClickEvents_ShortenedUrls");

        // Indexes for common queries
        clickEventBuilder.HasIndex(x => x.ShortenedUrlId)
            .HasDatabaseName("IX_ShortenedUrlId");
        clickEventBuilder.HasIndex(x => x.ClickedAtUtc)
            .HasDatabaseName("IX_ClickedAtUtc");
    }
}
