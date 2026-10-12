# URL Shortener — Implementation Plan

**Document Version:** 1.0  
**Status:** Approved for Phase 3 execution

## Overview

This document breaks down the implementation into logical, testable units organized by layer (Domain → Application → Infrastructure → API). Each section includes acceptance criteria and task dependencies.

---

## Task Structure

### Domain Layer (Lowest Risk, No Dependencies)

**Task Domain-1: Value Objects and Entities**
- Create `ShortCode` class (Value Object)
  - Properties: `Value: string` (6–8 alphanumeric, read-only)
  - Constructor validation: non-null, length check
  - Equality by value (override `Equals`, `GetHashCode`)
  - Unit test: valid/invalid codes, equality
- Create `ShortenedUrl` class (Entity/Aggregate Root)
  - Properties: `Id`, `ShortCode`, `OriginalUrl`, `CreatedAtUtc`
  - No behavior (initialization only)
  - Unit test: construction, property access

**Task Domain-2: Domain Services**
- Create `UrlValidator` class (Domain Service)
  - Method: `void Validate(string url)` (throws on error)
  - Validations: URI format, scheme (HTTP/HTTPS only), length (≤ 2048)
  - Unit tests:
	- Valid HTTP/HTTPS URLs
	- Invalid schemes (ftp, file, javascript)
	- Malformed URLs
	- URLs exceeding 2048 characters
	- Null/empty URL
- Create `ShortCodeGenerator` class (Domain Service)
  - Method: `string Generate()` (returns 6–8 random alphanumerics, Base62)
  - Uses `System.Security.Cryptography.RNGCryptoServiceProvider`
  - Unit tests:
	- Generated code is 6–8 characters
	- Only Base62 characters (a-z, A-Z, 0-9)
	- Randomness (multiple calls produce different codes)
	- No external I/O

---

### Application Layer (Defines Use Cases & Abstractions)

**Task App-1: DTOs and Validators**
- Create `CreateShortUrlRequest` DTO
  - Property: `OriginalUrl: string`
  - No validation here (just data holder)
- Create `CreateShortUrlResponse` DTO
  - Properties: `ShortCode`, `OriginalUrl`, `CreatedAtUtc`, `ShortUrl` (computed)
- Create `AnalyticsResponse` DTO
  - Properties: `ShortCode`, `OriginalUrl`, `TotalClicks`, `CreatedAtUtc`, `LastClickAtUtc?`
- Create `CreateShortUrlRequestValidator` class
  - Validates request: URL required, not whitespace, valid URI, ≤ 2048 chars
  - Unit tests: valid/invalid requests, error messages

**Task App-2: Repository Abstraction**
- Create `IUrlRepository` interface
  - Methods:
	- `Task<ShortenedUrl> CreateAsync(ShortenedUrl url, CancellationToken ct)`
	- `Task<ShortenedUrl?> GetByShortCodeAsync(string code, CancellationToken ct)`
	- `Task RecordClickAsync(Guid shortenedUrlId, CancellationToken ct)`
	- `Task<int> GetClickCountAsync(Guid shortenedUrlId, CancellationToken ct)`
	- `Task<DateTime?> GetLastClickAsync(Guid shortenedUrlId, CancellationToken ct)`
- Unit tests: use mock repository, verify repository contract calls

**Task App-3: Application Service (Use Case Orchestration)**
- Create `IShortUrlService` interface
  - Methods:
	- `Task<CreateShortUrlResponse> CreateAsync(CreateShortUrlRequest req, CancellationToken ct)`
	- `Task<ShortenedUrl> ResolveAndRecordClickAsync(string code, CancellationToken ct)`
	- `Task<AnalyticsResponse> GetAnalyticsAsync(string code, CancellationToken ct)`
- Create `ShortUrlService` implementation
  - `CreateAsync`:
	- Call validator (throw if invalid)
	- Generate short code
	- Loop (retry up to 5 times):
	  - Try `repository.CreateAsync(...)`
	  - On unique violation, retry
	  - On other errors, throw `CollisionExhaustionException` if exhausted
	- Map to response
  - `ResolveAndRecordClickAsync`:
	- Call `repository.GetByShortCodeAsync(code)`
	- If null, throw `NotFoundException`
	- Fire `repository.RecordClickAsync(...)` (async, non-blocking)
	- Log any click recording errors
	- Return ShortenedUrl
  - `GetAnalyticsAsync`:
	- Call `repository.GetByShortCodeAsync(code)`
	- If null, throw `NotFoundException`
	- Get click count and last click from repo
	- Map to response
- Unit tests (with mocked repository):
  - Valid URL creation, response structure
  - Invalid URL rejection
  - Collision retry succeeds
  - Collision retry exhaustion (409)
  - Redirect succeeds, URL returned
  - Redirect with click recording failure (redirect still works)
  - Unknown code returns null from repo → NotFoundException
  - Analytics aggregation (count, last click)

---

### Infrastructure Layer (Persistence Implementation)

**Task Infrastructure-1: EF Core DbContext and Models**
- Create `UrlShortenerDbContext` class
  - `DbSet<ShortenedUrl>`
  - `DbSet<ClickEvent>`
  - Configure in `OnModelCreating`:
	- ShortenedUrl: index on ShortCode, unique constraint, max lengths
	- ClickEvent: index on ShortenedUrlId, index on ClickedAtUtc, cascade delete
- Create migration: `dotnet ef migrations add InitialCreate`
  - Verify: creates ShortenedUrls and ClickEvents tables, indexes, constraints
- Unit/Integration tests:
  - DbContext can instantiate with test database
  - Tables created correctly
  - Indexes and constraints exist

**Task Infrastructure-2: Repository Implementation**
- Create `UrlRepository` class implements `IUrlRepository`
  - `CreateAsync`: Insert ShortenedUrl, catch unique violation → throw (caller retries)
  - `GetByShortCodeAsync`: Query DbSet with Where clause
  - `RecordClickAsync`: Insert ClickEvent
  - `GetClickCountAsync`: Count ClickEvents for URL with `_context.ClickEvents.CountAsync(...)`
  - `GetLastClickAsync`: Max ClickedAtUtc with `_context.ClickEvents.OrderByDescending(...).FirstOrDefaultAsync(...)`
- Error handling: wrap DB errors (SqlException, etc.) in application exceptions
- Integration tests (isolated SQLite database per test):
  - Insert and retrieve URL
  - Unique constraint violation on duplicate code
  - Click recording
  - Click aggregation (count, max)
  - Repository methods with cancellation token

---

### API Layer (HTTP Endpoints & DI)

**Task API-1: Dependency Injection Setup**
- Create `ServiceCollectionExtensions.cs` or `DependencyInjection.cs`
  - Register `IShortUrlService` → `ShortUrlService` (scoped)
  - Register `IUrlRepository` → `UrlRepository` (scoped)
  - Register `UrlValidator` (singleton or transient)
  - Register `ShortCodeGenerator` (singleton)
  - Register `CreateShortUrlRequestValidator` (transient)
- Update `Program.cs`:
  - Call extension to add services
  - Configure `DbContext` with SQLite connection string (from config)
  - Add logging (Console, optional Serilog)
  - Add OpenAPI/Swagger
  - Add exception middleware

**Task API-2: Exception Handling Middleware**
- Create `ExceptionHandlingMiddleware` class
  - Catches exceptions in pipeline
  - Maps exceptions to HTTP responses:
	- `ArgumentException`, `ArgumentNullException` → 400 Bad Request
	- `UrlValidationException`, custom validation errors → 400 Bad Request
	- `NotFoundException` → 404 Not Found
	- `CollisionExhaustionException` → 409 Conflict
	- All others → 500 Internal Server Error
  - Logs errors with context
  - Returns JSON error response (no stack traces)
- Error response format:
  ```json
  {
	"error": "Short code not found",
	"code": "NOT_FOUND",
	"statusCode": 404
  }
  ```

**Task API-3: Controllers and Endpoints**
- Create `UrlsController` class
  - `POST /api/urls` — create short URL
	- Accept `CreateShortUrlRequest`
	- Call `_service.CreateAsync(...)`
	- Return 201 Created with response
  - `GET /{code}` — redirect
	- Accept code as route parameter
	- Call `_service.ResolveAndRecordClickAsync(code)`
	- Return 302 Found with Location header
  - `GET /api/urls/{code}/analytics` — analytics
	- Accept code as route parameter
	- Call `_service.GetAnalyticsAsync(code)`
	- Return 200 OK with response
- Unit tests (with mock service):
  - Valid request → 201 with response
  - Invalid URL → 400 Bad Request
  - Redirect → 302 with correct Location
  - Unknown code → 404
  - Analytics → 200 with correct stats

**Task API-4: Health & Readiness Endpoints**
- Create health controller or inline in Program.cs
  - `GET /health` → `{ "status": "Healthy" }` (200 OK, always)
  - `GET /ready` → check database connectivity
	- If database accessible → `{ "status": "Ready" }` (200 OK)
	- If database unreachable → return 503 Service Unavailable
- Integration tests:
  - `/health` always returns 200
  - `/ready` returns 200 if database is connected
  - `/ready` returns 503 if database is unavailable

**Task API-5: OpenAPI/Swagger Configuration**
- Configure Swashbuckle in Program.cs
  - Document endpoints with XML comments and `[ProducesResponseType]`
  - Enable JSON response format
  - Swagger UI available at `/swagger/index.html`
- No critical test (documentation only, but good for demo)

---

## Task Execution Dependencies

```
Domain-1 (Entities, Value Objects)
  ↓
Domain-2 (Domain Services: UrlValidator, ShortCodeGenerator)
  ↓
App-1 (DTOs, Validators)
  ↓
App-2 (Repository Abstraction)
  ↓
App-3 (Application Service)
  ↓ (parallel with Infrastructure)
Infrastructure-1 (DbContext, Models, Migrations)
Infrastructure-2 (Repository Implementation)
  ↓
API-1 (DI Setup)
  ↓
API-2 (Exception Handling Middleware)
  ↓
API-3 (Controllers)
  ↓
API-4 (Health/Readiness)
  ↓
API-5 (OpenAPI)
```

**Parallelization:** Domain tasks can run independently. App and Infrastructure can run in parallel once Domain is complete. API depends on all layers.

---

## Testing Strategy (Integrated with Each Task)

### Unit Tests (Domain & Application)
- **Location:** `UrlShortener.UnitTests`
- **Scope:** Domain logic, services, validators (no database, no HTTP)
- **Test doubles:** Mock `IUrlRepository` in application services
- **Framework:** xUnit + Moq
- **Coverage target:** ≥ 80%

### Integration Tests (Infrastructure & API)
- **Location:** `UrlShortener.IntegrationTests`
- **Scope:** API contracts, database persistence, end-to-end workflows
- **Setup:** Isolated SQLite database per test (in-memory or temp file)
- **Test doubles:** Real repository with test DbContext
- **Framework:** xUnit + `WebApplicationFactory<T>` for API hosting
- **Coverage target:** ≥ 70% of API endpoints and workflows

### Acceptance Criteria per Task
- Domain-1: Unit tests for `ShortCode`, `ShortenedUrl` construction, equality
- Domain-2: Unit tests for `UrlValidator` (all rule combinations), `ShortCodeGenerator` (randomness, format)
- App-1: Unit tests for validators, request/response mapping
- App-2: Mock tests for repository contract
- App-3: Service unit tests with mocked repo (create, redirect, analytics workflows)
- Infrastructure-1: Integration test for DbContext, table creation, indexes
- Infrastructure-2: Integration tests for repository with real database
- API-1–5: Integration tests for full API workflows (create → redirect → analytics)

---

## Build & Validation Gate (Phase 3 Completion)

Once all tasks complete:
1. **Compile:** `dotnet build UrlShortener.sln --configuration Release` → 0 errors
2. **Test:** `dotnet test UrlShortener.sln --configuration Release` → all pass
3. **Format:** `dotnet format UrlShortener.sln --verify-no-changes` → pass
4. **Vulnerabilities:** `dotnet list UrlShortener.sln package --vulnerable` → no new vulnerabilities
5. **Manual validation:**
   - Run API, create URL via POST
   - Redirect via GET
   - Retrieve analytics
   - Kill database, verify `/ready` returns 503

---

## Risk & Mitigation

| Risk | Impact | Mitigation |
|---|---|---|
| EF Core migration issues | Medium | Test migrations in isolation; rollback scripts prepared |
| Collision handling not robust | Low | Bounded retries, comprehensive unit tests, stress test with parallel creates |
| Database constraint violation not caught | Medium | Try-catch in repository, specific exception type mapped |
| Async/cancellation bugs | Medium | CancellationToken tests, verify token passed through call chain |
| Exception middleware masks real errors | Medium | Detailed logging before wrapping; monitored error logs |

---

## Success Criteria (Phase 3 Exit)

- [x] All tasks implemented and code builds
- [x] Unit tests: ≥ 80% coverage for Domain & Application
- [x] Integration tests: ≥ 70% coverage for Infrastructure & API
- [x] All tests pass
- [x] Code formatted correctly
- [x] No vulnerable dependencies
- [x] API locally runnable and testable via HTTP
- [x] All four functional requirements (F1–F4) implemented
- [x] Non-functional requirements (logging, error handling, validation) met or documented

