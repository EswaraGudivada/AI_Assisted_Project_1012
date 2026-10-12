# Copilot Instructions for URL Shortener Development

## Principles

1. **Small, reviewable changes:** Each commit/PR touches ≤ 5 files, focused on one feature or fix
2. **Tests first:** Write tests before or alongside production code
3. **Security-first:** Validate all inputs, never log secrets, use parameterized queries
4. **Existing patterns:** Inspect existing code (architecture, naming, error handling) before adding new code
5. **Human review:** Ask for approval before breaking changes, high-impact decisions, or irreversible operations
6. **Truthful logging:** Never fabricate results, test outcomes, or engineering decisions; document actual findings

## Code Style

- Use file-scoped namespaces: `namespace UrlShortener.Domain;`
- Enable nullable reference types: `#nullable enable`
- Prefer async methods: `Task<T>` not `T`; always accept `CancellationToken`
- Use dependency injection: Abstractions registered in DI container, injected via constructor
- Avoid hardcoded strings: Use configuration, constants, or enums
- No `var` for unclear types; prefer explicit types or discards where appropriate

## Project Structure Invariants

- **Domain** → No external dependencies (only System.*)
- **Application** → Depends on Domain; defines abstractions and use cases
- **Infrastructure** → Implements Application abstractions; depends on EF Core, SQLite
- **API** → Depends on Application and Infrastructure; composes dependencies, hosts HTTP

## Testing Requirements

- Every public method has unit tests
- Every API route has integration tests
- Happy path + error path + edge cases
- Use mocks for external dependencies (repository, logging)
- Use real database for integration tests (isolated SQLite per test)
- Test names describe scenario and expected outcome

## Security Checklist

- [ ] Validate all user inputs (URL format, length, scheme)
- [ ] Never log passwords, API keys, or connection strings
- [ ] Use parameterized queries (EF Core does this automatically)
- [ ] Return generic error messages to clients (details only in logs)
- [ ] Check for open redirect vulnerabilities (validate URL scheme only)
- [ ] Verify no secrets in code, config, or version control

## Before Submitting a PR

1. Build passes: `dotnet build UrlShortener.sln --configuration Release`
2. All tests pass: `dotnet test UrlShortener.sln --configuration Release`
3. Code formatted: `dotnet format UrlShortener.sln --verify-no-changes`
4. No vulnerabilities: `dotnet list UrlShortener.sln package --vulnerable`
5. Documentation updated (README, architecture, if applicable)
6. Acceptance criteria met (from implementation plan or issue)

## Common Tasks

### Add a new API endpoint:
1. Define the service method in `IShortUrlService` (Application)
2. Implement in `ShortUrlService` (Application)
3. Write unit tests with mocked repository
4. Add controller action in `UrlsController` (API)
5. Add integration tests
6. Update docs/README with endpoint details

### Add a database migration:
1. Modify `UrlShortenerDbContext` entity model
2. Run: `dotnet ef migrations add MigrationName --project src/UrlShortener.Infrastructure`
3. Verify migration in `Migrations/` folder
4. Test with: `dotnet ef database update`
5. Write integration test verifying schema changes

### Fix a bug:
1. Create a test that reproduces the bug (test should fail)
2. Fix the bug
3. Verify the test now passes
4. Keep the test to prevent regression
5. Commit: "Fix: [description] (regression test in [file])"

### Refactor existing code:
1. Identify the existing pattern (inspect architecture.md)
2. Keep tests passing throughout refactor
3. Small commits (each commit buildable)
4. Update docs if architecture changes
5. Never skip tests or weak tests to finish faster

## When in Doubt

- Ask for approval before breaking changes
- Inspect existing code patterns first
- Write a test to validate your assumption
- Keep changes small and focused
- Document non-obvious decisions in code comments or docs

## Contacts & Escalation

- Architecture questions → Review docs/architecture.md
- Test failures → Check docs/testing-strategy.md
- Requirements ambiguities → See docs/requirements.md
- Build or deployment issues → Check .github/workflows/ci.yml

