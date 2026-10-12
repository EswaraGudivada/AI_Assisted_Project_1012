# URL Shortener — .NET 10

A secure, tested URL shortener implemented in C# with .NET 10, designed for high code quality, maintainability, and clear engineering judgment.

## Prerequisites

- **.NET 10.0.401 SDK** (verify with `dotnet --version`)
- **Git** for version control
- A text editor or IDE (Visual Studio 2022+, Visual Studio Code, JetBrains Rider)

## Setup

### 1. Clone and Restore

```bash
git clone <repository-url>
cd UrlShortener
dotnet restore UrlShortener.sln
```

### 2. Run Migrations

The application uses EF Core with SQLite. Migrations are applied automatically on startup, but you can apply them manually:

```bash
cd src/UrlShortener.Api
dotnet ef database update --configuration Release
```

The SQLite database is stored in the application working directory (defaulting to `url_shortener.db`).

### 3. Build

```bash
dotnet build UrlShortener.sln --configuration Release
```

### 4. Run

```bash
cd src/UrlShortener.Api
dotnet run --configuration Release
```

The API will be available at `http://localhost:5000` (HTTP) and `https://localhost:5001` (HTTPS).

## API Endpoints

### Health & Readiness

**GET /health**
```bash
curl http://localhost:5000/health
```

**GET /ready**
```bash
curl http://localhost:5000/ready
```

### Create a Short URL

**POST /api/urls**
```bash
curl -X POST http://localhost:5000/api/urls \
  -H "Content-Type: application/json" \
  -d '{"originalUrl":"https://www.example.com/very/long/path"}'
```

Response (201 Created):
```json
{
  "shortCode": "abc123",
  "originalUrl": "https://www.example.com/very/long/path",
  "createdAt": "2025-01-01T12:00:00Z",
  "shortUrl": "http://localhost:5000/abc123"
}
```

### Redirect to Original URL

**GET /{code}**
```bash
curl -L http://localhost:5000/abc123
```

This will redirect (302) to the original URL and record a click event.

### Get Analytics

**GET /api/urls/{code}/analytics**
```bash
curl http://localhost:5000/api/urls/abc123/analytics
```

Response:
```json
{
  "shortCode": "abc123",
  "originalUrl": "https://www.example.com/very/long/path",
  "totalClicks": 5,
  "createdAt": "2025-01-01T12:00:00Z",
  "lastClickAt": "2025-01-01T13:30:00Z"
}
```

## Testing

### Run All Tests

```bash
dotnet test UrlShortener.sln --configuration Release --logger "console;verbosity=normal"
```

### Run Unit Tests Only

```bash
dotnet test UrlShortener.UnitTests --configuration Release
```

### Run Integration Tests Only

```bash
dotnet test UrlShortener.IntegrationTests --configuration Release
```

### Run with Coverage

```bash
dotnet test UrlShortener.sln --configuration Release /p:CollectCoverage=true /p:CoverageFormat=opencover
```

## Quality Gates

### Build

```bash
dotnet build UrlShortener.sln --configuration Release
```

### Code Formatting

```bash
dotnet format UrlShortener.sln --verify-no-changes
```

### Dependency Vulnerability Check

```bash
dotnet list package --vulnerable
```

## Project Structure

- **src/UrlShortener.Api** — ASP.NET Core HTTP API, dependency composition, middleware
- **src/UrlShortener.Application** — Use cases, DTOs, application logic, service abstractions
- **src/UrlShortener.Domain** — Domain models, business rules, value objects
- **src/UrlShortener.Infrastructure** — EF Core data access, migrations, persistence
- **tests/UrlShortener.UnitTests** — Unit tests for application services, generators, validators
- **tests/UrlShortener.IntegrationTests** — API contracts, database persistence, end-to-end flows

## Architecture

See [docs/architecture.md](docs/architecture.md) for detailed design, assumptions, and trade-offs.

## Implementation Plan

See [docs/implementation-plan.md](docs/implementation-plan.md) for task decomposition and acceptance criteria.

## Requirements

See [docs/requirements.md](docs/requirements.md) for functional and non-functional requirements, constraints, and acceptance criteria.

## Testing Strategy

See [docs/testing-strategy.md](docs/testing-strategy.md) for test coverage, test doubles, and validation approach.

## Scenarios

See [docs/scenarios.md](docs/scenarios.md) for greenfield, brownfield, and ambiguous scope demonstrations and actual validation results.

## AI-Assisted Engineering Log

See [docs/ai-engineering-log.md](docs/ai-engineering-log.md) for task IDs, objectives, changes, and AI-assisted activity with validation results.

## Final Engineering Summary

See [docs/final-engineering-summary.md](docs/final-engineering-summary.md) for architecture review, implementation details, test results, build status, and remaining work.

## CI/CD

See [.github/workflows/ci.yml](.github/workflows/ci.yml) for automated build, test, and quality checks on every push and pull request.

## License

(Project-specific license, if applicable.)
"# AI_Assisted_Project_1012" 
