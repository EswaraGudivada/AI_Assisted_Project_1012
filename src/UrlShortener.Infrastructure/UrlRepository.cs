namespace UrlShortener.Infrastructure;

using Microsoft.EntityFrameworkCore;
using UrlShortener.Application;
using UrlShortener.Domain;

/// <summary>
/// EF Core implementation of IUrlRepository.
/// </summary>
public class UrlRepository : IUrlRepository
{
    private readonly UrlShortenerDbContext _context;

    public UrlRepository(UrlShortenerDbContext context)
    {
        _context = context;
    }

    public async Task<ShortenedUrl> CreateAsync(ShortenedUrl url, CancellationToken ct)
    {
        try
        {
            _context.ShortenedUrls.Add(url);
            await _context.SaveChangesAsync(ct);
            return url;
        }
        catch (DbUpdateException ex) when (ex.InnerException != null && ex.InnerException.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase))
        {
            // Unique constraint violation on ShortCode
            throw new InvalidOperationException("Short code is not unique", ex);
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Database error during URL creation", ex);
        }
    }

    public async Task<ShortenedUrl?> GetByShortCodeAsync(string shortCode, CancellationToken ct)
    {
        return await _context.ShortenedUrls
            .FirstOrDefaultAsync(x => x.ShortCode == shortCode, ct);
    }

    public async Task RecordClickAsync(Guid shortenedUrlId, CancellationToken ct)
    {
        var clickEvent = new ClickEvent
        {
            Id = Guid.NewGuid(),
            ShortenedUrlId = shortenedUrlId,
            ClickedAtUtc = DateTime.UtcNow
        };

        _context.ClickEvents.Add(clickEvent);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<int> GetClickCountAsync(Guid shortenedUrlId, CancellationToken ct)
    {
        return await _context.ClickEvents
            .CountAsync(x => x.ShortenedUrlId == shortenedUrlId, ct);
    }

    public async Task<DateTime?> GetLastClickAsync(Guid shortenedUrlId, CancellationToken ct)
    {
        return await _context.ClickEvents
            .Where(x => x.ShortenedUrlId == shortenedUrlId)
            .MaxAsync(x => (DateTime?)x.ClickedAtUtc, ct);
    }
}
