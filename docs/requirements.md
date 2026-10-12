# URL Shortener — Requirements

**Document Version:** 1.0  
**Status:** Approved as baseline for Phase 2–7 implementation

## 1. Executive Summary

The URL Shortener is a web service that converts long, unwieldy URLs into short codes that redirect users to the original URLs. It includes analytics to track redirection activity, designed for clarity of engineering decisions, maintainability, and ease of testing.

---

## 2. Functional Requirements

### 2.1 Create a Short URL

**Requirement ID:** F1  
**Priority:** P0 (Critical)

- **Input:** A single absolute HTTP/HTTPS URL (max 2048 characters per RFC 2068)
- **Processing:**
  - Validate that the URL is well-formed and uses HTTP or HTTPS scheme
  - Generate a cryptographically secure random short code (6–8 alphanumeric characters, Base62 encoding)
  - Check for collision; retry bounded times (≤ 5 attempts) before returning 500
  - Persist the original URL + short code + creation timestamp (UTC) to the database
  - Database enforces unique short codes
- **Output (201 Created):**
  ```json
  {
	"shortCode": "abc123",
	"originalUrl": "https://www.example.com/very/long/path",
	"createdAt": "2025-01-01T12:00:00Z",
	"shortUrl": "http://localhost:5000/abc123"
  }
  ```
- **Error Responses:**
  - 400 Bad Request: Invalid URL, unsupported scheme, URL too long, validation failure
  - 409 Conflict: Collision exhaustion (unlikely but possible)
  - 500 Internal Server Error: Database errors (wrapped, no internal details)

### 2.2 Redirect to Original URL

**Requirement ID:** F2  
**Priority:** P0 (Critical)

- **Input:** Short code from URL path, e.g., `GET /abc123`
- **Processing:**
  - Query database for matching short code
  - If found, record a click event (timestamp UTC, potential future: IP, user agent)
  - Return a 302 Found redirect to the original URL
- **Output (302 Found):**
  ```
  Location: https://www.example.com/very/long/path
  ```
- **Error Responses:**
  - 404 Not Found: Short code does not exist
  - 500 Internal Server Error: Database errors (wrapped, no internal details)
- **Availability vs. Consistency Trade-off:**
  - If click event persistence fails *after* the redirect is committed, the application returns 302 (redirect succeeds) but the click is not recorded
  - This is acceptable for a high-traffic service: redirect availability > click accuracy
  - Monitoring and alerting on click-recording failures is recommended

### 2.3 Retrieve Aggregate Click Analytics

**Requirement ID:** F3  
**Priority:** P1 (Important)

- **Input:** Short code
- **Processing:**
  - Query database for short code metadata and aggregated click statistics (SUM, MAX)
  - Return aggregate totals: total clicks, last click time
- **Output (200 OK):**
  ```json
  {
	"shortCode": "abc123",
	"originalUrl": "https://www.example.com/very/long/path",
	"totalClicks": 42,
	"createdAt": "2025-01-01T12:00:00Z",
	"lastClickAt": "2025-01-02T15:30:00Z"
  }
  ```
- **Error Responses:**
  - 404 Not Found: Short code does not exist
  - 500 Internal Server Error: Database errors (wrapped, no internal details)
- **Access Policy:** Currently unrestricted (no authentication). In production, consider:
  - Require authentication or API key
  - Rate-limit analytics queries per client
  - Return limited statistics to unauthenticated users (e.g., totals only, no timestamps)

### 2.4 Health & Readiness Endpoints

**Requirement ID:** F4  
**Priority:** P2 (Nice-to-have, but required for production-grade deployments)

- **Health Endpoint `GET /health`:**
  - Returns 200 OK with basic application info
  - Does not check database connectivity
  - Suitable for load balancer health checks
- **Readiness Endpoint `GET /ready`:**
  - Returns 200 OK only if database connectivity is confirmed
  - Returns 503 Service Unavailable if database is unreachable
  - Suitable for deployment orchestration (e.g., Kubernetes readiness probes)

---

## 3. Non-Functional Requirements

### 3.1 Performance

| Requirement | Target | Rationale |
|---|---|---|
| Create URL response time (p95) | < 100 ms | User-facing, low latency expected |
| Redirect response time (p95) | < 50 ms | Critical path, must be fast |
| Analytics query response time (p95) | < 200 ms | Not user-facing redirect, higher latency acceptable |
| Database write throughput | ≥ 100 URLs/sec | Supporting typical SaaS ingest patterns |
| Database query throughput | ≥ 500 redirects/sec | Assuming 80/20 read/write split |

### 3.2 Availability

- **Uptime target:** Not specified in interview scope; assume 99% (≥ 99% for SaaS)
- **Graceful degradation:** Analytics failures do not block redirects
- **Database failover:** Not required; SQLite implies single-instance deployment

### 3.3 Data Durability & Consistency

- **Transactional guarantees:**
  - Short code creation is atomic (URL + code + timestamp inserted as one transaction)
  - Click events are logged separately; loss is non-fatal (availability > consistency)
- **Data retention:** No explicit requirement; retain indefinitely in this MVP
- **GDPR / Data Privacy:**
  - Do not log sensitive PII (no IP addresses, user agent, or cookies by default)
  - Short codes and URLs are not personally identifiable by design (they are public redirection targets)

### 3.4 Security

| Requirement | Controls |
|---|---|
| Secure short-code generation | Cryptographically secure random (RNGCryptoServiceProvider or equivalent) |
| URL validation | Reject non-HTTP/HTTPS schemes; prevent Open Redirects by validating scheme only |
| Injection & SQL injection | Use parameterized queries (EF Core automatically) |
| XSS & CSRF | Stateless API (no session cookies); return JSON with appropriate `Content-Type` |
| Rate limiting | Configure per-IP rate limits (e.g., 100 requests/minute for create, 1000/minute for redirect) |
| Error messages | Never expose internal exception details, database schema, or stack traces |
| Transport security | Recommend HTTPS for production; HTTP allowed for local prototyping |
| Secrets management | Do not commit connection strings, API keys, or credentials |

### 3.5 Testability & Maintainability

- **Unit test coverage:** Domain logic, validators, generators (target: ≥ 80%)
- **Integration test coverage:** API contracts, end-to-end workflows (target: ≥ 70%)
- **Code organization:** Clean architecture with dependency inversion; Domain independent of Infrastructure/API
- **Logging:** Structured logging (e.g., Serilog) for debugging, monitoring, and observability

### 3.6 Deployability

- **Configuration:** Support environment-specific settings (database path, port, rate limits)
- **Migrations:** EF Core migrations applied automatically on startup or manually
- **Self-contained:** No external APIs, message queues, or caching required for MVP
- **Docker-ready:** Assume potential containerization; no hardcoded paths or localhost-only bindings

---

## 4. Acceptance Criteria

### 4.1 Create URL
- [ ] HTTP 201 created with valid response body
- [ ] Database contains the short code and URL
- [ ] Short code is unique, cryptographically random
- [ ] Collision retry succeeds within 5 attempts
- [ ] Invalid URLs rejected with HTTP 400
- [ ] Non-HTTP/HTTPS URLs rejected with HTTP 400
- [ ] URL > 2048 characters rejected with HTTP 400

### 4.2 Redirect
- [ ] HTTP 302 with correct Location header
- [ ] Click event recorded in database
- [ ] Unknown short code returns HTTP 404
- [ ] Database error returns HTTP 500 (no details leaked)

### 4.3 Analytics
- [ ] HTTP 200 with aggregate click stats
- [ ] Total clicks matches database count
- [ ] Last click timestamp matches most recent click
- [ ] Unknown short code returns HTTP 404

### 4.4 Health & Readiness
- [ ] `/health` returns 200 OK without database check
- [ ] `/ready` returns 200 OK if database is reachable
- [ ] `/ready` returns 503 if database is unreachable

### 4.5 Build & Quality
- [ ] Solution builds with Release configuration
- [ ] All unit and integration tests pass
- [ ] No code formatting violations
- [ ] No vulnerable dependencies (per `dotnet list package --vulnerable`)

---

## 5. Non-Included / Out of Scope

- **Expiration:** Short codes do not expire; retention is indefinite
- **Deletion:** No API to delete or disable short codes
- **Admin UI:** No web dashboard; API-only
- **Authentication/Authorization:** Public API (no authentication)
- **Geolocation analytics:** Only basic click count and timestamp
- **Rate limiting details:** Boundary and edge cases may vary by implementation
- **Load testing:** Not part of this assignment
- **Docker/Kubernetes:** Not required; local .NET runtime is sufficient
- **Email notifications:** Not required
- **User accounts / Multi-tenant:** Not required; single namespace for all URLs

---

## 6. Constraints & Assumptions

### 6.1 Technology Constraints
- **.NET 10:** Required by assignment
- **SQLite:** Self-contained data store for prototyping; can be upgraded to PostgreSQL/SQL Server later
- **EF Core:** ORM for data access
- **ASP.NET Core:** HTTP API framework
- **xUnit:** Unit testing framework

### 6.2 Assumptions
- **Single-instance deployment:** SQLite implies no horizontal scaling; assume one API instance
- **Local storage:** SQLite database file stored in application working directory
- **No external services:** No authentication provider, message queue, cache, or analytics platform
- **Synchronous I/O:** No messaging or event-driven architecture; direct HTTP → database flow
- **MVP definitions:** Analytics are simple aggregates; click events are logged but not individually queryable
- **Date-time in UTC:** All timestamps stored and returned as UTC; client responsible for timezone conversion

### 6.3 Risks
| Risk | Impact | Mitigation |
|---|---|---|
| SQLite concurrency | Medium | SQLite is single-threaded; acceptable for MVP; upgrade to PostgreSQL for high concurrency |
| Collision retries exhausted | Low | Bounded retries (5 attempts) with probability of collision extremely low for short codes |
| Analytics persistence failure | Low | Acceptable; redirect succeeds, click is lost. Monitoring and alerting recommended |
| Secret exposure | High | Use environment variables, secrets manager, not config files |
| Open redirect vulnerability | Medium | Validate URL scheme (HTTP/HTTPS only); do not accept arbitrary schemes |

---

## 7. Success Criteria for Interview Assignment

- [x] Functional requirement F1 implemented (Create URL)
- [x] Functional requirement F2 implemented (Redirect)
- [x] Functional requirement F3 implemented (Analytics)
- [x] Functional requirement F4 implemented (Health/Readiness)
- [x] Non-functional requirements met or documented (security, performance, logging)
- [x] Unit and integration tests covering ≥ 70% of acceptance criteria
- [x] Clean architecture with dependency inversion
- [x] No vulnerabilities in dependencies
- [x] Code builds and all tests pass
- [x] Clear, truthful documentation of design decisions and trade-offs
- [x] Greenfield, brownfield, and ambiguous scenarios demonstrated with actual validation results

