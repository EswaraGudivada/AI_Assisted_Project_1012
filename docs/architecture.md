# URL Shortener — Architecture

**Document Version:** 1.0  
**Status:** Approved baseline for implementation

## 1. Architecture Overview

The URL Shortener uses a **modular monolith** architecture with clean separation of concerns following **Onion Architecture** (also called Ports & Adapters or Hexagonal Architecture). This enables:

- **Domain isolation:** Core business logic depends on no infrastructure
- **Testability:** Application services can be unit-tested without databases
- **Maintainability:** Clear responsibility boundaries and easy evolution
- **Flexibility:** Infrastructure can be swapped (e.g., SQLite → PostgreSQL) without touching business logic

---

## 2. Layered Architecture

```
┌─────────────────────────────────────────────┐
│         API Layer (Presentation)             │
│  UrlShortener.Api — ASP.NET Core             │
│  ├─ Controllers                              │
│  ├─ DependencyInjection.cs                  │
│  ├─ ExceptionHandling Middleware             │
│  ├─ Program.cs (Startup)                     │
└─────────────────────────────────────────────┘
		  ↓ Uses                              
┌─────────────────────────────────────────────┐
│     Application Layer (Use Cases)            │
│  UrlShortener.Application — DTOs, Services  │
│  ├─ CreateShortUrlService                   │
│  ├─ ResolveShortUrlService                  │
│  ├─ RecordClickService                      │
│  ├─ GetAnalyticsService                     │
│  ├─ Abstractions (IUrlRepository, ...)      │
│  ├─ DTOs (CreateShortUrlRequest/Response)   │
│  └─ Validators                              │
└─────────────────────────────────────────────┘
		  ↓ Uses                              
┌─────────────────────────────────────────────┐
│     Domain Layer (Business Rules)            │
│  UrlShortener.Domain — Models, Value Objs   │
│  ├─ ShortenedUrl (Entity)                    │
│  ├─ ShortCode (Value Object)                 │
│  ├─ ClickEvent (Value Object)                │
│  ├─ UrlValidator (Domain Service)            │
│  └─ ShortCodeGenerator (Domain Service)      │
└─────────────────────────────────────────────┘
		  ↑ Depends on (Abstraction)          
┌─────────────────────────────────────────────┐
│   Infrastructure Layer (Persistence)        │
│  UrlShortener.Infrastructure — EF Core      │
│  ├─ UrlShortenerDbContext                   │
│  ├─ UrlShortenerRepository                  │
│  ├─ Migrations                              │
│  └─ Configuration                           │
└─────────────────────────────────────────────┘
```

### Dependency Flow

**Golden Rule:** Dependencies always point inward toward the Domain.

- **Domain** → zero dependencies (can be used in Console apps, Tests)
- **Application** → depends on Domain only
- **Infrastructure** → depends on Application (implements abstractions) and Domain
- **API** → depends on Application and Infrastructure (composes dependencies)

This ensures:
- Domain logic never couples to HTTP, SQL, or external libraries
- Application logic is framework-agnostic and unit-testable
- Infrastructure can be replaced without touching business logic

---

## 3. Key Components & Responsibilities

### 3.1 Domain Layer (`UrlShortener.Domain`)

**Entities:**
- `ShortenedUrl` — aggregate root
  - Properties: `Id` (Guid), `ShortCode` (string), `OriginalUrl` (string), `CreatedAtUtc` (DateTime)
  - Methods: None (no behavior; mostly a data holder)

**Value Objects:**
- `ShortCode` — represents a valid short code
  - Guarantees: non-null, 6–8 alphanumeric characters
  - Equality: By value (two ShortCodes with same value are equal)
- `ClickEvent` — represents a redirect event
  - Properties: `Id` (Guid), `ShortenedUrlId` (Guid), `ClickedAtUtc` (DateTime)
  - Used for time-series analytics

**Domain Services:**
- `ShortCodeGenerator` — generates cryptographically secure random short codes
  - Method: `Generate() → string`
  - Dependency: `System.Security.Cryptography.RNGCryptoServiceProvider`
- `UrlValidator` — validates URL format and scheme
  - Method: `Validate(url: string) → bool` + throws `InvalidOperationException` on error
  - Validation rules: HTTP(S) only, valid URI format, max 2048 chars

### 3.2 Application Layer (`UrlShortener.Application`)

**DTOs (Data Transfer Objects):**
- `CreateShortUrlRequest` — input model
  - `OriginalUrl: string` (validated server-side)
- `CreateShortUrlResponse` — output model
  - `ShortCode: string`
  - `OriginalUrl: string`
  - `CreatedAtUtc: DateTime`
  - `ShortUrl: string` (computed URL, e.g., `http://localhost:5000/{code}`)
- `AnalyticsResponse` — output model
  - `ShortCode: string`
  - `OriginalUrl: string`
  - `TotalClicks: int`
  - `CreatedAtUtc: DateTime`
  - `LastClickAtUtc?: DateTime` (nullable if no clicks)

**Application Services (Use Cases):**
- `IShortUrlService` (interface) / `ShortUrlService` (implementation)
  - `CreateAsync(request, cancellationToken) → CreateShortUrlResponse` — orchestrates URL validation, code generation, persistence
  - `ResolveAndRecordClickAsync(code, cancellationToken) → ShortenedUrl` — finds URL, records click
  - `GetAnalyticsAsync(code, cancellationToken) → AnalyticsResponse` — aggregates click stats

**Abstractions (Interfaces):**
- `IUrlRepository` — persistence adapter
  - `CreateAsync(shortenedUrl, cancellationToken) → ShortenedUrl`
  - `GetByShortCodeAsync(code, cancellationToken) → ShortenedUrl?`
  - `RecordClickAsync(shortenedUrlId, cancellationToken) → void`
  - `GetClickCountAsync(shortenedUrlId, cancellationToken) → int`
  - `GetLastClickAsync(shortenedUrlId, cancellationToken) → DateTime?`

**Validators:**
- `CreateShortUrlRequestValidator` — FluentValidation or manual rules
  - URL not null, not empty, valid URI, HTTP/HTTPS only, max 2048 chars

### 3.3 Infrastructure Layer (`UrlShortener.Infrastructure`)

**EF Core DbContext:**
- `UrlShortenerDbContext` — manages ShortenedUrl and ClickEvent entities
  - `DbSet<ShortenedUrl>` 
  - `DbSet<ClickEvent>`
  - Configures indexes, constraints, mappings

**Repository Implementation:**
- `UrlRepository` — implements `IUrlRepository`
  - Uses `UrlShortenerDbContext` to persist and query
  - Handles concurrency (unique constraint on ShortCode)
  - Translates domain exceptions to application exceptions

**Migrations:**
- Initial migration creates tables, indexes, unique constraints
- Managed via `dotnet ef` tooling

**Database Configuration:**
- SQLite database file: `url_shortener.db` (configurable)
- Connection string typically injected via configuration

### 3.4 API Layer (`UrlShortener.Api`)

**Controllers:**
- `UrlsController` — RESTful endpoints
  - `POST /api/urls` — create short URL
  - `GET /{code}` — redirect (not typical REST, but handled here)
  - `GET /api/urls/{code}/analytics` — retrieve analytics

**Middleware:**
- `ExceptionHandlingMiddleware` — catches exceptions and returns wrapped error responses
  - `ValidationException` → 400 Bad Request
  - `NotFoundException` → 404 Not Found
  - Generic exceptions → 500 Internal Server Error (without details)

**Startup Composition:**
- `Program.cs` — configures services, middleware, logging, OpenAPI
- `DependencyInjectionExtension.cs` — registers repositories, services, validators
- Configures Serilog (or similar) for structured logging

**Health Endpoints:**
- `/health` — simple health check (200 OK)
- `/ready` — readiness check (verifies database, returns 503 if unavailable)

---

## 4. Request Flow Diagrams

### 4.1 Create Short URL

```
POST /api/urls
{ "originalUrl": "https://example.com/..." }
		 ↓
UrlsController.CreateAsync()
		 ↓
ShortUrlService.CreateAsync()
  ├─ UrlValidator.Validate(url)  [Domain Service]
  ├─ ShortCodeGenerator.Generate()  [Domain Service, retry loop ≤5 times]
  ├─ IUrlRepository.CreateAsync(shortenedUrl)  [Abstraction]
  │  └─ UrlRepository (Infrastructure) [EF Core insert]
  │     └─ SQL: INSERT INTO ShortenedUrls (Id, ShortCode, OriginalUrl, CreatedAtUtc) VALUES (...)
  └─ Map to CreateShortUrlResponse
		 ↓
201 Created
{ "shortCode": "abc123", "originalUrl": "...", "createdAt": "...", "shortUrl": "..." }
```

### 4.2 Redirect to Original URL

```
GET /abc123
		 ↓
UrlsController.GetAsync(code)  [Catches all routes except explicit /api/...]
		 ↓
ShortUrlService.ResolveAndRecordClickAsync(code)
  ├─ IUrlRepository.GetByShortCodeAsync(code)  [Abstraction]
  │  └─ UrlRepository (Infrastructure) [EF Core select]
  │     └─ SQL: SELECT * FROM ShortenedUrls WHERE ShortCode = @code
  ├─ If not found: throw NotFoundException
  ├─ IUrlRepository.RecordClickAsync(shortenedUrlId)  [Abstraction]
  │  └─ UrlRepository (Infrastructure) [EF Core insert]
  │     └─ SQL: INSERT INTO ClickEvents (Id, ShortenedUrlId, ClickedAtUtc) VALUES (...)
  │     └─ [If fails, log error; redirect still returned]
  └─ Return shortenedUrl
		 ↓
302 Found
Location: https://example.com/...
```

### 4.3 Get Analytics

```
GET /api/urls/{code}/analytics
		 ↓
UrlsController.GetAnalyticsAsync(code)
		 ↓
ShortUrlService.GetAnalyticsAsync(code)
  ├─ IUrlRepository.GetByShortCodeAsync(code)  [Abstraction]
  │  └─ UrlRepository (Infrastructure) [EF Core select]
  ├─ If not found: throw NotFoundException
  ├─ IUrlRepository.GetClickCountAsync(shortenedUrlId)  [Abstraction]
  │  └─ UrlRepository (Infrastructure) [EF Core aggregate]
  │     └─ SQL: SELECT COUNT(*) FROM ClickEvents WHERE ShortenedUrlId = @id
  ├─ IUrlRepository.GetLastClickAsync(shortenedUrlId)  [Abstraction]
  │  └─ UrlRepository (Infrastructure) [EF Core max]
  │     └─ SQL: SELECT MAX(ClickedAtUtc) FROM ClickEvents WHERE ShortenedUrlId = @id
  └─ Map to AnalyticsResponse
		 ↓
200 OK
{ "shortCode": "abc123", "originalUrl": "...", "totalClicks": 42, "createdAt": "...", "lastClickAt": "..." }
```

---

## 5. Cross-Cutting Concerns

### 5.1 Logging

**Strategy:** Structured logging via Serilog (or Microsoft.Extensions.Logging abstractions)

**Levels:**
- **ERROR:** Database failures, unexpected exceptions → includes operation context, not sensitive data
- **WARNING:** Collision retry exhaustion, rate limit threshold → actionable alerts
- **INFORMATION:** Request start/end (optional), startup/shutdown events
- **DEBUG:** URL validation details, code generation attempts (disabled in Release)

**Example:**
```csharp
_logger.LogInformation("Creating short URL for {UrlLength} characters", originalUrl.Length);
_logger.LogWarning("Short code collision retry count: {Attempts}/{MaxAttempts}", attempts, MaxRetries);
_logger.LogError(exception, "Failed to create short URL after {Attempts} retries", MaxRetries);
```

### 5.2 Exception Handling

**Domain Exceptions:**
- `InvalidOperationException` — validation failure, collision exhaustion

**Application Exceptions (custom):**
- `UrlValidationException` — URL format invalid
- `CollisionExhaustionException` — could not generate unique code
- `NotFoundException` — short code not found

**Middleware Handling:**
- Catches all exceptions and returns structured error responses
- No stack traces, internal details, or exception types exposed to client
- All errors logged for internal debugging

### 5.3 Validation

**Validation Layers:**
1. **API Layer (Synchronous):**
   - Model binding, attribute-based validators (e.g., `[Required]`, `[StringLength]`)
   - Basic type checking, null-checking

2. **Application Layer (Business Rules):**
   - `UrlValidator` checks URI format, scheme, length
   - `CreateShortUrlRequestValidator` orchestrates validation
   - Returns detailed error messages for client

3. **Database Layer (Constraints):**
   - Unique constraint on `ShortCode` (prevents duplicates)
   - NOT NULL constraints on required fields
   - Enforced by database engine as last line of defense

### 5.4 Rate Limiting

**Configuration (to be implemented in Phase 3):**
- Global rate limits per IP address (e.g., 100 create requests/minute, 1000 redirects/minute)
- Returned as HTTP 429 Too Many Requests with `Retry-After` header
- Configurable via appsettings.json

### 5.5 Cancellation

**Async Cancellation:**
- All async methods accept `CancellationToken`
- Propagated through repository, DbContext operations
- Allows graceful shutdown and long-operation cancellation

---

## 6. Data Model

### 6.1 ShortenedUrl Table

```sql
CREATE TABLE ShortenedUrls (
  Id                    UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
  ShortCode             NVARCHAR(10) NOT NULL UNIQUE,
  OriginalUrl           NVARCHAR(2048) NOT NULL,
  CreatedAtUtc          DATETIME NOT NULL DEFAULT GETUTCDATE(),
  INDEX IX_ShortCode ON ShortenedUrls(ShortCode)
);
```

**Rationale:**
- `Id` (GUID): Surrogate key for relationships and internal references
- `ShortCode`: Unique, indexed, query column for redirects
- `OriginalUrl`: Full URL stored, max 2048 chars per RFC
- `CreatedAtUtc`: Audit trail, analytics
- `Index on ShortCode`: Speed up lookups (most common query)

### 6.2 ClickEvent Table

```sql
CREATE TABLE ClickEvents (
  Id                    UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
  ShortenedUrlId        UNIQUEIDENTIFIER NOT NULL FOREIGN KEY,
  ClickedAtUtc          DATETIME NOT NULL DEFAULT GETUTCDATE(),
  INDEX IX_ShortenedUrlId ON ClickEvents(ShortenedUrlId),
  INDEX IX_ClickedAtUtc ON ClickEvents(ClickedAtUtc),
  CONSTRAINT FK_ClickEvents_ShortenedUrls
	FOREIGN KEY (ShortenedUrlId) REFERENCES ShortenedUrls(Id) ON DELETE CASCADE
);
```

**Rationale:**
- `Id`: Event identifier for internal tracking
- `ShortenedUrlId`: Foreign key to parent shortened URL
- `ClickedAtUtc`: When the click occurred (for time-series analytics, last-click queries)
- `Indexes`: Speed up aggregations (COUNT, MAX) and cascading deletes
- `Cascade delete`: If a URL is deleted, its events are automatically removed (data consistency)

---

## 7. Transaction & Concurrency Strategy

### 7.1 Create Short URL (Transactional)

**Goal:** Atomically insert URL + code or fail without partial state.

**Flow:**
1. Validate URL (checks before DB interaction, no transaction overhead)
2. Generate short code
3. Begin transaction:
   - Insert ShortenedUrl (with generated code)
   - Commit transaction
4. If unique constraint violation (collision), **retry** from step 2 (up to 5 times)
5. If max retries exhausted, return 409 Conflict

**Concurrency Considerations:**
- Two threads generating the same code and both attempting insert → one succeeds, one gets unique violation → second retries
- SQLite handles locks; optimistic locks not needed (simplifies code)

### 7.2 Record Click (Eventual Consistency)

**Goal:** Record click event; if it fails, redirect still succeeds (availability > consistency).

**Flow:**
1. Insert ClickEvent (fire-and-forget or with retry)
2. Return 302 redirect *regardless* of click record success
3. Log error if click recording fails (alerting and debugging)

**Rationale:**
- Click events are telemetry, not critical to core functionality
- User experience (fast redirect) > perfect click count
- Monitoring alerts on click-recording failures

### 7.3 Get Analytics (Read Consistency)

**Goal:** Aggregate clicks; no locking needed (read-only operation).

**Flow:**
1. Query ShortenedUrl
2. Aggregate COUNT and MAX(ClickedAtUtc) from ClickEvents
3. Return results (may be slightly stale if writes are in flight)

**Concurrency Considerations:**
- SQLite read locks are minimal; no row-level locking
- Results are eventual consistent (good for analytics use case)

---

## 8. Error Handling & Resilience

### 8.1 Expected Errors (Handled)

| Scenario | Status | Action |
|---|---|---|
| URL validation fails | 400 Bad Request | Return validation error, log at INFO |
| Short code collision (retries exhausted) | 409 Conflict | Return error, log at WARNING |
| Database insert fails (other reasons) | 500 Internal Server Error | Log error, return generic msg, alert |
| Short code not found | 404 Not Found | Return not found, log at INFO |
| Database query fails | 500 Internal Server Error | Log error, return generic msg |
| Click recording fails | 302 Found (redirect succeeds) | Log error at WARNING, alert (analytics failure) |
| Database unavailable (ready endpoint) | 503 Service Unavailable | Indicate problems, block new requests |

### 8.2 Resilience Patterns

- **Retry on collision:** Up to 5 attempts, exponential backoff optional
- **Graceful degradation:** Analytics fail → redirect still works
- **Structured error responses:** Client receives consistent error format, no details
- **Monitoring:** All errors logged with context for incident response

---

## 9. Alternative Architectures & Trade-Offs

### 9.1 Alternative: Three-Tier (Presentation ↔ Business ↔ Data)

**Pros:** Simpler, less indirection  
**Cons:** Harder to test business logic (coupled to Data layer), less flexible for future changes  
**Decision:** Rejected in favor of Onion Architecture (better testability)

### 9.2 Alternative: Event-Driven (Async Clicks via Message Queue)

**Pros:** Decouples click recording from redirect, better scalability  
**Cons:** Added complexity, introduces eventual consistency issues, requires RabbitMQ/Kafka  
**Decision:** Rejected for MVP (synchronous model sufficient and simpler to test)

### 9.3 Alternative: Redis for Click Caching

**Pros:** Reduced database load for click recording  
**Cons:** Introduces external dependency, complexity, data loss risk  
**Decision:** Rejected for MVP (SQLite sufficient; can add later if needed)

### 9.4 Database Choice: SQLite vs. PostgreSQL

| Aspect | SQLite | PostgreSQL |
|---|---|---|
| Setup | Embedded file | External server required |
| Concurrency | Single-threaded | Full MVCC support |
| Scaling | Single instance | Horizontal scaling |
| Transactions | ACID | ACID |
| Cost | Free | Free (self-hosted) |
| Best for | Prototyping, single-instance | Production SaaS, high concurrency |

**Decision:** SQLite for MVP (self-contained, simple); upgrade to PostgreSQL for production (handles concurrent redirects).

---

## 10. Deployment & Configuration

### 10.1 Configuration Sources (Priority Order)

1. **Environment variables** (highest priority, safe for secrets)
   - `DATABASE_PATH` — SQLite file location
   - `ASPNETCORE_ENVIRONMENT` — Development | Production
   - `LOG_LEVEL` — Logging level

2. **appsettings.json** (application defaults)
   - Rate limits, code length, retry count

3. **appsettings.{Environment}.json** (environment overrides)
   - Production-specific database connection, logging settings

### 10.2 Startup Sequence

1. Load configuration (environment variables override appsettings)
2. Configure dependency injection (register services, repositories, validators)
3. Configure logging
4. Configure EF Core (attach DbContext, set connection string)
5. Build WebApplication
6. Run migrations (if configured)
7. Start listening on configured port

### 10.3 Shutdown Sequence

- Graceful shutdown on SIGTERM
- Allow in-flight requests to complete (5-second timeout)
- Close database connections
- Log shutdown event

---

## 11. Observability

### 11.1 Logging

**Structured logging fields:**
```json
{
  "timestamp": "2025-01-01T12:00:00Z",
  "level": "INFORMATION",
  "logger": "UrlShortener.Application.ShortUrlService",
  "message": "Creating short URL",
  "urlLength": 85,
  "traceId": "0HN1N2V48SVQD:00000001"
}
```

### 11.2 Metrics (Future)

- Endpoint latency (p50, p95, p99)
- Error rate by type
- Database query time
- Click recording success/failure rate

### 11.3 Health Checks

- `/health` — basic liveness
- `/ready` — readiness (includes database check)

---

## 12. Security Considerations

### 12.1 Open Redirect Prevention

**Risk:** User clicks `https://shortener.example.com/abc123`, redirects to arbitrary external URL  
**Prevention:** Validate URL scheme (HTTP/HTTPS only) during creation; no user-supplied redirect targets  
**Code:**
```csharp
if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
	(uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
{
	throw new UrlValidationException("Invalid URL scheme");
}
```

### 12.2 Short Code Enumeration

**Risk:** Attacker guesses short codes and accesses URLs  
**Mitigation:** Cryptographically random codes (6–8 chars, Base62 = 62^6 = 56 billion possibilities)  
**Analysis:** Even with 1000 guesses/second, median time to collision is ~1 billion seconds (31 years)  
**Acceptance:** Acceptable for public URLs; if codes must be secret, use longer codes or API authentication

### 12.3 Injection Prevention

- **SQL Injection:** EF Core parameterized queries prevent injection (no string concatenation)
- **XSS:** JSON API responses; no user input reflected in HTML
- **CSRF:** Stateless API; no session cookies; each request includes full context

### 12.4 Secrets Management

- **Database connection strings:** Use environment variables or Azure Key Vault (not in code)
- **API keys (future):** Never log full keys; redact in logs
- **Example:**
  ```csharp
  // WRONG: connection string in appsettings.json
  // RIGHT: environment variable
  var connStr = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING");
  ```

---

## 13. Observability & Monitoring (Production Checklist)

- [x] Structured logging with correlation IDs
- [x] Health and readiness endpoints
- [ ] Application Insights or Datadog integration (Phase 5+)
- [ ] Database query performance monitoring
- [ ] Alerting on error rate, latency p95, collision retry exhaustion
- [ ] Audit trail for data access (if PII involved)

---

## 14. Future Enhancements (Out of Scope)

- **Custom short codes:** Allow user to specify code (e.g., `/my-campaign`)
- **URL expiration:** Codes expire after N days
- **Deletion/Deactivation:** API to disable short codes
- **Authentication:** Require API key or OAuth2
- **Admin dashboard:** Web UI for analytics and management
- **Geolocation analytics:** Track clicks by country
- **A/B testing:** Multiple URLs per short code with probability distribution
- **Rate limiting:** Per-client or global thresholds
- **Caching:** Redis for click aggregation
- **Horizontal scaling:** Multiple API instances, PostgreSQL, load balancer
- **CDN:** Serve redirects from edge locations
- **Webhook notifications:** Alert owners on URL creation/click milestones

---

## 15. Summary

The architecture prioritizes:
1. **Domain isolation** (Onion) for business logic clarity and testability
2. **Simplicity** (no premature optimization, external services, or complex patterns)
3. **Extensibility** (abstractions allow upgrades without rewriting)
4. **Security** (validation, error handling, secrets management)
5. **Observability** (logging, health checks, error tracing)

The design is suitable for a prototype and can scale to production with database and caching upgrades; no architectural overhaul needed.
