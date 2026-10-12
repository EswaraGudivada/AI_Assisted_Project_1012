# URL Shortener — Testing Strategy

**Document Version:** 1.0  
**Status:** Baseline for Phase 4 test implementation

## 1. Overview

This document defines the testing approach for the URL Shortener, ensuring correctness, reliability, and maintainability through unit and integration tests.

---

## 2. Testing Pyramid

```
	   ╱╲
	  ╱  ╲  Integration Tests (70% coverage)
	 ╱────╲ ├─ API endpoints
	╱      ╲ ├─ Database persistence
   ╱────────╲ └─ End-to-end workflows
  ╱──────────╲
 ╱────────────╲  Unit Tests (80% coverage)
╱──────────────╲ ├─ Domain logic
╱────────────────╲ ├─ Application services
╱──────────────────╲ ├─ Validators
╱────────────────────╲ └─ Generators
```

---

## 3. Unit Testing Strategy

### 3.1 Scope

**What to test:** Domain logic, application services, validators, generators  
**What NOT to test:** HTTP, database, file I/O, external APIs

### 3.2 Test Structure (Arrange-Act-Assert)

```csharp
[Fact]
public void GenerateShortCode_ProducesValidCode()
{
	// Arrange
	var generator = new ShortCodeGenerator();

	// Act
	var code = generator.Generate();

	// Assert
	Assert.NotNull(code);
	Assert.Equal(6, code.Length);  // or 8, depending on implementation
	Assert.Matches(@"^[a-zA-Z0-9]+$", code);  // Base62 characters
}
```

### 3.3 Domain Layer Tests

#### ShortCode Value Object Tests

```
UnitTests/Domain/ShortCodeTests.cs
├─ Constructor_WithValidCode_Succeeds
├─ Constructor_WithNullCode_Throws
├─ Constructor_WithEmptyCode_Throws
├─ Constructor_WithInvalidLength_Throws
├─ Constructor_WithNonBase62Chars_Throws
├─ Equality_WithSameValue_AreEqual
├─ Inequality_WithDifferentValues_AreNotEqual
└─ GetHashCode_WithSameValue_SameHash
```

#### ShortenedUrl Entity Tests

```
UnitTests/Domain/ShortenedUrlTests.cs
├─ Constructor_WithValidInputs_Succeeds
├─ Constructor_WithNullShortCode_Throws
├─ Constructor_WithNullOriginalUrl_Throws
├─ CreatedAtUtc_IsSetToCurrentUtcTime
└─ Properties_AreReadable
```

#### UrlValidator Domain Service Tests

```
UnitTests/Domain/UrlValidatorTests.cs
├─ Validate_WithValidHttpUrl_Succeeds
├─ Validate_WithValidHttpsUrl_Succeeds
├─ Validate_WithFtpUrl_Throws_UnsupportedScheme
├─ Validate_WithFileUrl_Throws_UnsupportedScheme
├─ Validate_WithJavascriptUrl_Throws_UnsupportedScheme
├─ Validate_WithMalformedUrl_Throws_Invalid
├─ Validate_WithNullUrl_Throws
├─ Validate_WithEmptyUrl_Throws
├─ Validate_WithUrlExceeding2048Chars_Throws_UrlTooLong
├─ Validate_WithUrlExactly2048Chars_Succeeds
├─ ErrorMessages_AreClear_AndActionable
└─ Validate_WithInternalNavigation_Succeeds  // Edge case: http://192.168.1.1
```

#### ShortCodeGenerator Domain Service Tests

```
UnitTests/Domain/ShortCodeGeneratorTests.cs
├─ Generate_ProducesAlphanumericCode
├─ Generate_ProducesCodeLength_Between_6_And_8
├─ Generate_ProducesRandomCode_OnMultipleCalls
├─ Generate_AllCharacters_AreBase62  // a-z, A-Z, 0-9
├─ Generate_NoTraceOfInputState_InOutput  // Verify randomness, not deterministic
└─ Generate_DistributionOfCharacters_IsReasonablyRandom  // Statistical test (optional)
```

### 3.4 Application Layer Tests

#### CreateShortUrlRequestValidator Tests

```
UnitTests/Application/CreateShortUrlRequestValidatorTests.cs
├─ Validate_WithValidRequest_Succeeds
├─ Validate_WithNullUrl_Fails
├─ Validate_WithEmptyUrl_Fails
├─ Validate_WithWhitespaceUrl_Fails
├─ Validate_WithInvalidUri_Fails
├─ Validate_WithNonHttpScheme_Fails
├─ Validate_WithUrlExceeding2048Chars_Fails
└─ ErrorMessages_ContainPropertyAndReason
```

#### ShortUrlService Tests (With Mocked Repository)

```
UnitTests/Application/ShortUrlServiceTests.cs

├─ CreateAsync_WithValidUrl_ReturnsCreatedResponse
│  ├─ Mocked: IUrlRepository.CreateAsync returns created entity
│  └─ Assert: Response contains shortCode, originalUrl, createdAt, shortUrl
├─ CreateAsync_WithInvalidUrl_ThrowsUrlValidationException
├─ CreateAsync_OnUniqueConstraintViolation_RetriesAndSucceeds
│  ├─ Mock setup: CreateAsync throws unique violation 2x, succeeds 3rd
│  └─ Assert: Method retries at least 3 times
├─ CreateAsync_AfterMaxRetries_ThrowsCollisionExhaustionException
│  ├─ Mock setup: CreateAsync always throws unique violation
│  └─ Assert: Exception thrown after 5 attempts
├─ CreateAsync_OnOtherDbError_ThrowsImmediately
├─ CreateAsync_GeneratedCodeIsUnique_EachCall
│  ├─ Mock call tracking to verify different codes on retry
│  └─ Assert: ShortCodeGenerator.Generate called multiple times
│
├─ ResolveAndRecordClickAsync_WithValidCode_ReturnsUrl
│  ├─ Mock: GetByShortCodeAsync returns valid URL
│  └─ Assert: Method calls GetByShortCodeAsync and RecordClickAsync
├─ ResolveAndRecordClickAsync_WithUnknownCode_ThrowsNotFoundException
│  ├─ Mock: GetByShortCodeAsync returns null
│  └─ Assert: NotFoundException thrown, no click recorded
├─ ResolveAndRecordClickAsync_OnClickRecordingFailure_StillReturnsUrl
│  ├─ Mock: RecordClickAsync throws, but GetByShortCodeAsync succeeds
│  └─ Assert: Exception logged, URL still returned (availability > consistency)
├─ ResolveAndRecordClickAsync_ClickRecordingError_IsLogged
│  └─ Assert: ILogger.LogError called with exception
│
├─ GetAnalyticsAsync_WithValidCode_ReturnsAggregates
│  ├─ Mock: GetByShortCodeAsync returns URL, GetClickCountAsync returns 42, GetLastClickAsync returns timestamp
│  └─ Assert: Response contains correct totals
├─ GetAnalyticsAsync_WithUnknownCode_ThrowsNotFoundException
├─ GetAnalyticsAsync_WithNoClicks_ReturnsZeroClicksNullLastClick
│  ├─ Mock: GetClickCountAsync returns 0, GetLastClickAsync returns null
│  └─ Assert: Response has TotalClicks=0, LastClickAt=null
└─ GetAnalyticsAsync_CorrectlyAggregatesClickData
   └─ Assert: Service queries repository for aggregates (verifies SUM/MAX logic)
```

### 3.5 Unit Test Best Practices

- **Isolation:** Mock all external dependencies (`IUrlRepository`, logging)
- **Determinism:** No random data; seed or mock randomness
- **Clarity:** Test names describe scenario and expected outcome
- **Single assertion focus:** One logical assertion per test (may have multiple Assert calls)
- **AAA pattern:** Clear Arrange → Act → Assert sections
- **No shared state:** Each test independent, no `@BeforeEach` side effects

### 3.6 Unit Test Coverage Goals

| Layer | Target Coverage | Rationale |
|---|---|---|
| Domain logic | ≥ 85% | Core business rules, high priority |
| Application services | ≥ 80% | Use cases, including error paths |
| Validators | ≥ 90% | All validation rules and edge cases |
| Generators | ≥ 85% | Randomness, format, edge cases |
| Infrastructure (unit) | ≥ 70% | Mostly covered by integration tests |
| API controllers | ≥ 60% | Mostly covered by integration tests |

---

## 4. Integration Testing Strategy

### 4.1 Scope

**What to test:** API endpoints, database persistence, end-to-end workflows, error scenarios  
**What NOT to test:** External services, network failures, third-party libraries

### 4.2 Test Database Setup

**Approach:** Isolated SQLite database per test

```csharp
public class UrlShortenerIntegrationTests : IAsyncLifetime
{
	private string _dbPath;
	private UrlShortenerDbContext _dbContext;

	public async Task InitializeAsync()
	{
		_dbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
		// Set up fresh database for each test
		var options = new DbContextOptionsBuilder<UrlShortenerDbContext>()
			.UseSqlite($"Data Source={_dbPath}")
			.Options;
		_dbContext = new UrlShortenerDbContext(options);
		await _dbContext.Database.MigrateAsync();
	}

	public async Task DisposeAsync()
	{
		await _dbContext.DisposeAsync();
		File.Delete(_dbPath);
	}
}
```

### 4.3 API Integration Tests (WebApplicationFactory)

```
IntegrationTests/Api/UrlsControllerTests.cs

├─ POST /api/urls
│  ├─ CreateShortUrl_WithValidUrl_Returns201Created
│  ├─ CreateShortUrl_WithValidUrl_ResponseContainsCorrectFields
│  ├─ CreateShortUrl_WithInvalidUrl_Returns400BadRequest
│  ├─ CreateShortUrl_WithUnsupportedScheme_Returns400BadRequest
│  ├─ CreateShortUrl_WithUrlExceeding2048Chars_Returns400BadRequest
│  ├─ CreateShortUrl_AfterCollisionRetries_Returns201Created
│  ├─ CreateShortUrl_AfterCollisionExhaustion_Returns409Conflict
│  └─ CreateShortUrl_OnDatabaseError_Returns500InternalServerError
│
├─ GET /{code}
│  ├─ Redirect_WithValidCode_Returns302Found
│  ├─ Redirect_WithValidCode_LocationHeaderCorrect
│  ├─ Redirect_WithValidCode_RecordsClickEvent
│  ├─ Redirect_WithUnknownCode_Returns404NotFound
│  ├─ Redirect_OnDatabaseError_Returns500InternalServerError
│  ├─ Redirect_OnClickRecordingFailure_Still Returns302
│  └─ Redirect_MultipleRedirects_EachRecordsClickEvent
│
├─ GET /api/urls/{code}/analytics
│  ├─ GetAnalytics_WithValidCode_Returns200Ok
│  ├─ GetAnalytics_Response_ContainsCorrectFields
│  ├─ GetAnalytics_WithMultipleClicks_CorrectslyAggregates
│  ├─ GetAnalytics_WithNoClicks_ReturnsZeroAndNullTimestamp
│  ├─ GetAnalytics_WithUnknownCode_Returns404NotFound
│  └─ GetAnalytics_OnDatabaseError_Returns500InternalServerError
│
└─ Health & Readiness
   ├─ GetHealth_Always_Returns200Ok
   ├─ GetReady_WithAccessibleDatabase_Returns200Ok
   └─ GetReady_WithInaccessibleDatabase_Returns503ServiceUnavailable
```

### 4.4 Database Persistence Tests

```
IntegrationTests/Infrastructure/UrlRepositoryTests.cs

├─ CreateAsync_WithValidUrl_SavesAndReturns
├─ CreateAsync_WithDuplicateCode_ThrowsUniqueViolation
│  ├─ Manually insert URL, attempt duplicate
│  └─ Assert: DbUpdateException or specific constraint violation
├─ GetByShortCodeAsync_WithValidCode_ReturnsUrl
├─ GetByShortCodeAsync_WithUnknownCode_ReturnsNull
├─ GetByShortCodeAsync_CaseInsensitive_FindsCode  // or case-sensitive, depending on design
├─ RecordClickAsync_WithValidId_InsertsClickEvent
├─ RecordClickAsync_WithInvalidId_ThrowsFK_Violation  // or handled gracefully
├─ GetClickCountAsync_WithNoClicks_ReturnsZero
├─ GetClickCountAsync_WithMultipleClicks_ReturnsCorrectCount
├─ GetLastClickAsync_WithNoClicks_ReturnsNull
├─ GetLastClickAsync_WithMultipleClicks_ReturnsLatestTimestamp
└─ ClickEvents_OnUrlDelete_AreAutomaticallyDeleted  // Cascade delete
```

### 4.5 End-to-End Workflow Tests

```
IntegrationTests/Workflows/UrlShortenerE2ETests.cs

├─ FullWorkflow_CreateRedirectAnalytics
│  ├─ POST /api/urls with valid URL → 201 with shortCode
│  ├─ GET /{code} → 302 redirect
│  ├─ GET /{code} again → 302 redirect (2nd click)
│  └─ GET /api/urls/{code}/analytics → 200 with totalClicks=2, correct lastClick
├─ FullWorkflow_MultipleUrlsTrackedIndependently
│  ├─ Create 3 URLs with different codes
│  ├─ Redirect each multiple times
│  └─ Verify analytics for each show correct click counts
├─ ErrorScenario_InvalidUrlRejected_NoDataCreated
│  ├─ POST /api/urls with invalid URL
│  ├─ Assert: 400 returned, database empty
│  └─ Assert: Attempting redirect for non-existent code returns 404
├─ ErrorScenario_UnknownCode_NoClickRecorded
│  ├─ POST /api/urls with valid URL
│  ├─ GET with different code → 404
│  └─ Assert: Analytics for original URL still shows 0 clicks
└─ Concurrency_ParallelCreatesWithCollision
   ├─ Spawn 10 threads, each trying to create URL to same short code
   ├─ Assert: Only 1 succeeds, others get conflict or retry
   └─ Assert: Database contains exactly 1 entry
```

### 4.6 Error Scenario Tests

```
IntegrationTests/ErrorHandling/ExceptionHandlingTests.cs

├─ ValidationError_Returns400_WithoutStackTrace
├─ NotFoundError_Returns404_WithoutDetails
├─ CollisionExhaustion_Returns409_WithoutDetails
├─ DatabaseError_Returns500_WithoutInternalDetails
│  ├─ Verify: Error message does not expose schema, connection string, stack trace
│  └─ Assert: Internal error logged, but not returned to client
├─ ErrorResponse_HasConsistentFormat
│  └─ Assert: All errors contain { error, code, statusCode }
└─ MissingContentType_Returns400_BadRequest
```

### 4.7 Configuration & Rate Limiting Tests (If Implemented)

```
IntegrationTests/Configuration/RateLimitingTests.cs

├─ RateLimit_ExceededCreate_Returns429
├─ RateLimit_ExceededRedirect_Returns429
├─ RateLimit_WithValidRetryAfter_Header
└─ RateLimit_PerIP_Tracks_Separately
```

### 4.8 Integration Test Best Practices

- **Isolation:** Fresh database per test via `IAsyncLifetime`
- **No external I/O:** Only SQLite, no HTTP calls to other services
- **Realistic workflows:** Test sequences of API calls, not isolated endpoints
- **Error path coverage:** 404, 400, 500, edge cases
- **Concurrency:** At least one test with parallel operations
- **Rollback:** No test leaves data affecting subsequent tests
- **Readability:** Clear test names, comments for setup

### 4.9 Integration Test Framework & Setup

```csharp
// Program.cs or test setup
services.AddScoped<IUrlRepository, UrlRepository>();
services.AddScoped<IShortUrlService, ShortUrlService>();
services.AddSingleton<UrlValidator>();
services.AddSingleton<ShortCodeGenerator>();
services.AddDbContext<UrlShortenerDbContext>(options =>
	options.UseSqlite(_testDbConnectionString));
```

---

## 5. Test Execution

### 5.1 Run All Tests

```bash
dotnet test UrlShortener.sln --configuration Release
```

### 5.2 Run by Project

```bash
dotnet test UrlShortener.UnitTests --configuration Release
dotnet test UrlShortener.IntegrationTests --configuration Release
```

### 5.3 Run by Category

```bash
dotnet test --filter "Category=Domain"
dotnet test --filter "Category=Application"
dotnet test --filter "Category=Infrastructure"
dotnet test --filter "Category=Api"
```

### 5.4 Run with Coverage

```bash
dotnet test UrlShortener.sln /p:CollectCoverage=true /p:CoverageFormat=opencover
```

---

## 6. Test Data & Fixtures

### 6.1 Valid Test URLs

```csharp
public static class TestData
{
	public static string ValidHttpUrl => "http://example.com";
	public static string ValidHttpsUrl => "https://example.com/path?query=value";
	public static string LongValidUrl => new string('a', 2048);
	public static string UrlExceeding2048 => new string('a', 2049);
	public static string InternalNetworkUrl => "http://192.168.1.1/admin";
}
```

### 6.2 Invalid Test URLs

```csharp
public static string InvalidSchemeUrl => "ftp://example.com";
public static string MalformedUrl => "not a url";
public static string JavascriptUrl => "javascript:alert('xss')";
public static string FileUrl => "file:///etc/passwd";
```

### 6.3 Test Builders

```csharp
public class ShortenedUrlBuilder
{
	public static ShortenedUrl BuildDefault() =>
		new ShortenedUrl { Id = Guid.NewGuid(), ShortCode = "abc123", ... };
}
```

---

## 7. Regression Testing

Whenever a defect is discovered:

1. Create a failing test that reproduces the bug
2. Fix the code
3. Verify the test now passes
4. Keep the test in the suite to prevent regression

Example:
```csharp
[Fact]
public void Regression_ShortCodeWithLeadingZero_IsPreserved()
{
	// Bug: Leading zeros in code were stripped
	var code = "0abc12";
	var shortCode = new ShortCode(code);
	Assert.Equal(code, shortCode.Value);
}
```

---

## 8. Test Metrics & Gates

| Metric | Target | Action if Not Met |
|---|---|---|
| Unit test pass rate | 100% | Fix test or code |
| Integration test pass rate | 100% | Fix test or code |
| Code coverage (Domain + App) | ≥ 80% | Add additional tests |
| Build time | < 30 seconds | Optimize, cache, or parallelize |
| Test execution time | < 60 seconds (all) | Parallelize, mock slow operations |

---

## 9. Continuous Integration

Tests run automatically on every commit via GitHub Actions (see `.github/workflows/ci.yml`):

```yaml
- name: Run Tests
  run: dotnet test UrlShortener.sln --configuration Release --logger "trx" --results-directory ./results

- name: Upload Test Results
  if: always()
  uses: actions/upload-artifact@v2
  with:
	name: test-results
	path: ./results
```

---

## 10. Known Test Limitations & Assumptions

- **SQLite concurrency:** SQLite is single-threaded; high-concurrency scenarios may behave differently in PostgreSQL
- **Randomness:** `ShortCodeGenerator` tests use probability; extremely rare edge cases may not be caught
- **Performance:** Integration tests do not load-test; throughput/latency targets not validated
- **Network:** No network partition or latency simulation
- **Date-time:** Tests assume system clock is accurate; mock `DateTime.UtcNow` if needed

---

## 11. Test Review Checklist

Before marking tests as complete, verify:

- [ ] All acceptance criteria have corresponding tests
- [ ] Tests are isolated (no interdependencies)
- [ ] Test names clearly describe scenario and expectation
- [ ] All happy-path scenarios tested
- [ ] All error-path scenarios tested
- [ ] Edge cases covered (nulls, empty, boundary values)
- [ ] Tests run consistently (no flakiness)
- [ ] Build + test execution < 60 seconds
- [ ] No hardcoded file paths or environment assumptions
- [ ] No lingering TODO or SKIP markers

---

## Summary

The testing strategy ensures:
1. **Correctness:** Domain logic, application flows, API contracts verified
2. **Reliability:** Error cases and edge cases covered
3. **Maintainability:** Tests serve as living documentation
4. **Speed:** Build and test cycle < 60 seconds
5. **Confidence:** ≥ 80% coverage + comprehensive integration tests = high confidence in releases

