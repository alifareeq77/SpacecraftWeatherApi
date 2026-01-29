# Architecture

## Overview

SpacecraftWeatherApi is an ASP.NET Core 9.0 Web API that proxies weather data from a configurable third-party service. It adds resilience (timeout + retry via Polly), persists every successful response to a local SQLite database, and falls back to the most recent cached response when the upstream service is unavailable.

## Request Flow

```
Client
  |
  v
WeatherController          GET /api/weather
  |
  v
IWeatherService
  |
  v
WeatherService
  |--- Polly pipeline (timeout + exponential-backoff retry)
  |       |
  |       v
  |    HttpClient --> Third-party weather API
  |                   (URL, auth, timeout all from config / env vars)
  |
  |-- on success --> IWeatherRepository.SaveAsync()  --> SQLite
  |                  return JSON to controller
  |
  |-- on failure --> IWeatherRepository.GetLatestAsync() --> SQLite
  |                  return cached JSON (or null / 204)
  v
Controller returns 200 + JSON  or  204 No Content
```

## Project Structure

```
SpacecraftWeatherApi/
├── Configuration/
│   └── WeatherServiceOptions.cs    # Strongly-typed options (URL, timeout, retry, auth)
├── Controllers/
│   └── WeatherController.cs        # Single GET endpoint
├── Data/
│   ├── Entities/
│   │   └── WeatherRecord.cs        # EF Core entity (Id, RawJson, FetchedAt)
│   └── WeatherDbContext.cs          # DbContext with WeatherRecords DbSet
├── Repositories/
│   ├── IWeatherRepository.cs       # Interface: SaveAsync, GetLatestAsync
│   └── WeatherRepository.cs        # EF Core implementation
├── Services/
│   ├── IWeatherService.cs          # Interface: GetWeatherDataAsync
│   └── WeatherService.cs           # Core logic: fetch, validate, persist, fallback
├── Program.cs                      # Entry point, DI registration, middleware pipeline
├── appsettings.json                # Default configuration
└── appsettings.Development.json    # Development overrides

SpacecraftWeatherApi.Tests/
├── Controllers/
│   └── WeatherControllerTests.cs   # 3 unit tests (mocked service)
├── Services/
│   └── WeatherServiceTests.cs      # 4 unit tests (stub HTTP + Polly, mocked repo)
└── Repositories/
    └── WeatherRepositoryTests.cs   # 4 integration tests (in-memory SQLite)
```

## Layers

### Controller

`WeatherController` is a thin layer. It calls `IWeatherService.GetWeatherDataAsync()`, returns the raw JSON string with `Content-Type: application/json` on success, or `204 No Content` when no data is available.

### Service

`WeatherService` contains the core business logic:

1. Builds an `HttpRequestMessage` with optional auth headers from configuration.
2. Executes the HTTP call inside a Polly resilience pipeline (timeout + retry).
3. Validates the response is well-formed JSON via `JsonDocument.Parse`.
4. Persists the raw JSON to the repository on success.
5. On any exception, falls back to the latest cached record from the repository.

### Repository

`WeatherRepository` wraps EF Core operations against `WeatherDbContext`:

- `SaveAsync` -- creates a `WeatherRecord` with the raw JSON and a UTC timestamp.
- `GetLatestAsync` -- returns the most recent record ordered by `FetchedAt` descending.

### Database

SQLite via EF Core. The schema has a single table:

| Column | Type | Notes |
|--------|------|-------|
| `Id` | `long` | Primary key, auto-increment |
| `RawJson` | `string` | Full JSON response from upstream |
| `FetchedAt` | `DateTime` | UTC timestamp, indexed for fast latest-record lookup |

The database is auto-migrated on startup in `Program.cs`.

## Resilience

Configured via Polly in `Program.cs`:

| Strategy | Default | Config Key |
|----------|---------|------------|
| Timeout | 4 000 ms | `WeatherService:TimeoutMs` |
| Retry attempts | 2 | `WeatherService:RetryCount` |
| Retry base delay | 200 ms | `WeatherService:RetryBaseDelayMs` |
| Backoff | Exponential | -- |

Retries trigger on `HttpRequestException`, `TaskCanceledException`, `TimeoutRejectedException`, and `InvalidOperationException`.

When all retries are exhausted (or any other exception occurs), the service falls back to the most recent cached response from SQLite.

## Configuration & Environment Variables

All settings live under the `WeatherService` section in `appsettings.json` and can be overridden with environment variables using the `__` (double-underscore) separator:

| Setting | appsettings key | Environment variable | Default |
|---------|----------------|---------------------|---------|
| Upstream URL | `WeatherService:Url` | `WeatherService__Url` | *(empty)* |
| Request timeout | `WeatherService:TimeoutMs` | `WeatherService__TimeoutMs` | `4000` |
| Retry count | `WeatherService:RetryCount` | `WeatherService__RetryCount` | `2` |
| Retry base delay | `WeatherService:RetryBaseDelayMs` | `WeatherService__RetryBaseDelayMs` | `200` |
| Auth scheme | `WeatherService:AuthScheme` | `WeatherService__AuthScheme` | *(empty)* |
| Auth token | `WeatherService:AuthToken` | `WeatherService__AuthToken` | *(empty)* |
| API key | `WeatherService:ApiKey` | `WeatherService__ApiKey` | *(empty)* |
| SQLite conn string | `ConnectionStrings:WeatherDb` | `ConnectionStrings__WeatherDb` | `Data Source=weather.db` |

### Authentication headers

Headers are attached per-request only when their values are non-empty:

- **Authorization** -- sent as `Authorization: {AuthScheme} {AuthToken}` when both `AuthScheme` and `AuthToken` are set.
- **X-Api-Key** -- sent as `X-Api-Key: {ApiKey}` when `ApiKey` is set.

Both can be used simultaneously.

## Dependency Injection

Registered in `Program.cs`:

| Registration | Lifetime | Notes |
|---|---|---|
| `WeatherServiceOptions` | Options pattern | Bound to `WeatherService` config section |
| `WeatherDbContext` | Scoped | SQLite via EF Core |
| Polly pipeline `"weather"` | Singleton | Timeout + retry |
| `IWeatherService` / `WeatherService` | Transient | Typed `HttpClient` via `AddHttpClient` |
| `IWeatherRepository` / `WeatherRepository` | Scoped | EF Core data access |

## Testing

11 xUnit tests across three layers:

- **Controller tests (3)** -- mock `IWeatherService`, verify HTTP status codes and content type.
- **Service tests (4)** -- stub `DelegatingHandler` for HTTP, no-op Polly pipeline, mock `IWeatherRepository`. Covers success, HTTP failure with cache, HTTP failure without cache, and invalid JSON fallback.
- **Repository tests (4)** -- real in-memory SQLite (`DataSource=:memory:`), no mocks. Covers persist, timestamp, latest-record ordering, and empty DB.

```bash
dotnet test SpacecraftWeatherApi.Tests/SpacecraftWeatherApi.Tests.csproj
```

## API Surface

| Method | Route | Response |
|--------|-------|----------|
| `GET` | `/api/weather` | `200` with JSON body, or `204` if no data available |

Swagger UI available in Development at `/swagger`. OpenAPI spec at `/openapi/v1.json`.
