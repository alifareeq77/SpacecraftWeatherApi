# SpacecraftWeatherApi - Code Documentation

This document provides a line-by-line explanation of each file in the SpacecraftWeatherApi project and explains how they are linked together.

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────┐
│                              Program.cs                                  │
│                    (Application Entry Point & DI Setup)                  │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
         ┌──────────────────────────┼──────────────────────────┐
         │                          │                          │
         ▼                          ▼                          ▼
┌─────────────────┐    ┌─────────────────────┐    ┌─────────────────────┐
│ WeatherController│    │   WeatherService    │    │  WeatherDbContext   │
│   (API Layer)    │───▶│  (Business Logic)   │───▶│   (Data Access)     │
└─────────────────┘    └─────────────────────┘    └─────────────────────┘
                                │                          │
                                ▼                          ▼
                       ┌─────────────────┐        ┌─────────────────┐
                       │WeatherRepository│        │  WeatherRecord  │
                       │ (Persistence)   │───────▶│   (Entity)      │
                       └─────────────────┘        └─────────────────┘
```

---

## 1. SpacecraftWeatherApi.csproj

**Purpose:** Project configuration file that defines the build settings and dependencies.

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
```
- **Line 1:** Declares this as an ASP.NET Core Web project using the Web SDK.

```xml
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
```
- **Line 3-7:** Project properties:
  - `TargetFramework`: Builds for .NET 9.0
  - `Nullable`: Enables nullable reference types for null safety
  - `ImplicitUsings`: Auto-imports common namespaces (System, System.Linq, etc.)

```xml
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="9.0.12" />
```
- **Line 10:** OpenAPI/Swagger document generation support.

```xml
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="9.0.2">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
```
- **Line 11-14:** EF Core design-time tools for migrations (not included in published output).

```xml
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="9.0.2" />
```
- **Line 15:** SQLite database provider for Entity Framework Core.

```xml
    <PackageReference Include="Polly.Extensions" Version="8.6.5" />
```
- **Line 16:** Polly library for resilience patterns (retry, timeout, circuit breaker).

```xml
    <PackageReference Include="Swashbuckle.AspNetCore.SwaggerUI" Version="10.1.0" />
```
- **Line 17:** Swagger UI for interactive API documentation.

---

## 2. appsettings.json

**Purpose:** Application configuration file containing settings for logging, database, and external service.

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
```
- **Lines 1-7:** Logging configuration:
  - `Default`: Log all messages at Information level and above
  - `Microsoft.AspNetCore`: Only log Warning and above for framework internals

```json
  "AllowedHosts": "*",
```
- **Line 8:** Accepts requests from any host (used by Host Filtering Middleware).

```json
  "ConnectionStrings": {
    "WeatherDb": "Data Source=weather.db"
  },
```
- **Lines 9-11:** SQLite connection string. Creates `weather.db` file in the project directory.
- **Link:** Used by `Program.cs` line 22 to configure `WeatherDbContext`.

```json
  "WeatherService": {
    "Url": "https://webhook.site/32c81568-32ba-4909-86b6-f5d0661921dd",
    "TimeoutMs": 4000,
    "RetryCount": 2,
    "RetryBaseDelayMs": 200,
    "AuthScheme": "",
    "AuthToken": "",
    "ApiKey": ""
  }
```
- **Lines 12-20:** Weather service configuration:
  - `Url`: External API endpoint to fetch weather data
  - `TimeoutMs`: Request timeout (4 seconds)
  - `RetryCount`: Number of retry attempts on failure
  - `RetryBaseDelayMs`: Base delay for exponential backoff (200ms, 400ms, etc.)
  - `AuthScheme/AuthToken`: Optional Bearer/Basic authentication
  - `ApiKey`: Optional API key header
- **Link:** Maps to `WeatherServiceOptions` class (Configuration folder).

---

## 3. Program.cs

**Purpose:** Application entry point using top-level statements. Configures dependency injection and HTTP pipeline.

```csharp
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;
using Polly.Timeout;
using SpacecraftWeatherApi.Configuration;
using SpacecraftWeatherApi.Data;
using SpacecraftWeatherApi.Repositories;
using SpacecraftWeatherApi.Services;
```
- **Lines 1-8:** Import namespaces:
  - EF Core for database operations
  - Polly for resilience patterns
  - Project namespaces for configuration, data, repositories, and services

```csharp
var builder = WebApplication.CreateBuilder(args);
```
- **Line 10:** Creates the web application builder with default configuration (loads appsettings.json, environment variables, etc.).

```csharp
// Configuration
builder.Services.Configure<WeatherServiceOptions>(
    builder.Configuration.GetSection(WeatherServiceOptions.SectionName));
```
- **Lines 12-14:** Binds the "WeatherService" section from appsettings.json to `WeatherServiceOptions` class.
- **Link:** `WeatherServiceOptions.SectionName` = "WeatherService" (defined in Configuration/WeatherServiceOptions.cs).

```csharp
var weatherOptions = builder.Configuration
    .GetSection(WeatherServiceOptions.SectionName)
    .Get<WeatherServiceOptions>() ?? new WeatherServiceOptions();
```
- **Lines 16-18:** Reads weather options for use in Polly configuration. Uses defaults if section is missing.

```csharp
// EF Core + SQLite
builder.Services.AddDbContext<WeatherDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("WeatherDb")));
```
- **Lines 20-22:** Registers `WeatherDbContext` with SQLite using the "WeatherDb" connection string.
- **Link:** `WeatherDbContext` is defined in Data/WeatherDbContext.cs.

```csharp
// Polly resilience pipeline
builder.Services.AddResiliencePipeline<string, string>("weather", pipelineBuilder =>
{
    pipelineBuilder
        .AddTimeout(new TimeoutStrategyOptions
        {
            Timeout = TimeSpan.FromMilliseconds(weatherOptions.TimeoutMs)
        })
```
- **Lines 24-31:** Creates a named resilience pipeline "weather":
  - Adds timeout strategy (4000ms by default).

```csharp
        .AddRetry(new RetryStrategyOptions<string>
        {
            MaxRetryAttempts = weatherOptions.RetryCount,
            Delay = TimeSpan.FromMilliseconds(weatherOptions.RetryBaseDelayMs),
            BackoffType = DelayBackoffType.Exponential,
            ShouldHandle = new PredicateBuilder<string>()
                .Handle<HttpRequestException>()
                .Handle<TaskCanceledException>()
                .Handle<TimeoutRejectedException>()
                .Handle<InvalidOperationException>()
        });
});
```
- **Lines 32-43:** Adds retry strategy:
  - 2 retry attempts with exponential backoff (200ms, 400ms)
  - Handles HTTP errors, cancellation, timeout, and invalid operations
- **Link:** Pipeline used by `WeatherService.GetWeatherDataAsync()` line 36.

```csharp
// HttpClient + WeatherService (typed client handles IWeatherService registration)
builder.Services.AddHttpClient<IWeatherService, WeatherService>();
```
- **Line 46:** Registers `WeatherService` as a typed HTTP client implementing `IWeatherService`.
- **Link:** Injects `HttpClient` into `WeatherService` constructor.

```csharp
// Repositories
builder.Services.AddScoped<IWeatherRepository, WeatherRepository>();
```
- **Line 49:** Registers `WeatherRepository` as scoped (one instance per HTTP request).
- **Link:** `IWeatherRepository` injected into `WeatherService`.

```csharp
// Controllers & OpenAPI
builder.Services.AddControllers();
builder.Services.AddOpenApi();
```
- **Lines 51-53:** Registers MVC controllers and OpenAPI document generation.

```csharp
var app = builder.Build();
```
- **Line 55:** Builds the configured application.

```csharp
// Auto-migrate database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<WeatherDbContext>();
    db.Database.Migrate();
}
```
- **Lines 57-62:** Applies pending EF Core migrations automatically on startup.
- **Link:** Runs migration from Migrations/20260128080442_InitialCreate.cs.

```csharp
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Spacecraft Weather API v1");
    });
}
```
- **Lines 64-72:** In Development environment:
  - Maps OpenAPI endpoint at `/openapi/v1.json`
  - Enables Swagger UI for testing

```csharp
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
```
- **Lines 74-78:** HTTP pipeline:
  - Redirects HTTP to HTTPS
  - Enables authorization middleware
  - Maps controller routes
  - Starts the application

---

## 4. Configuration/WeatherServiceOptions.cs

**Purpose:** Strongly-typed configuration class for weather service settings.

```csharp
namespace SpacecraftWeatherApi.Configuration;
```
- **Line 1:** Declares namespace for configuration classes.

```csharp
public class WeatherServiceOptions
{
    public const string SectionName = "WeatherService";
```
- **Lines 3-5:** Class with constant for JSON section name.
- **Link:** Used in Program.cs lines 13-14, 17 to bind configuration.

```csharp
    public string Url { get; set; } = string.Empty;
    public int TimeoutMs { get; set; } = 4000;
    public int RetryCount { get; set; } = 2;
    public int RetryBaseDelayMs { get; set; } = 200;
```
- **Lines 7-10:** Core settings with sensible defaults:
  - `Url`: External API endpoint
  - `TimeoutMs`: 4 second timeout
  - `RetryCount`: 2 retries
  - `RetryBaseDelayMs`: 200ms base delay
- **Link:** Used in Program.cs for Polly configuration, WeatherService for HTTP requests.

```csharp
    /// <summary>
    /// Authorization scheme (e.g. "Bearer", "Basic"). Sent as the Authorization header when both this and AuthToken are set.
    /// </summary>
    public string AuthScheme { get; set; } = string.Empty;

    /// <summary>
    /// Authorization token/credential value. Sent as the Authorization header when both this and AuthScheme are set.
    /// </summary>
    public string AuthToken { get; set; } = string.Empty;

    /// <summary>
    /// API key sent via the X-Api-Key header when set.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
}
```
- **Lines 12-26:** Authentication options:
  - `AuthScheme` + `AuthToken`: For Authorization header (Bearer/Basic)
  - `ApiKey`: For X-Api-Key header
- **Link:** Used in WeatherService.cs lines 42-46 to set request headers.

---

## 5. Controllers/WeatherController.cs

**Purpose:** API controller that exposes the weather endpoint.

```csharp
using Microsoft.AspNetCore.Mvc;
using SpacecraftWeatherApi.Services;
```
- **Lines 1-2:** Import MVC framework and services namespace.

```csharp
namespace SpacecraftWeatherApi.Controllers;
```
- **Line 4:** Controllers namespace.

```csharp
[ApiController]
[Route("api/[controller]")]
public class WeatherController : ControllerBase
```
- **Lines 6-8:** Controller attributes:
  - `[ApiController]`: Enables automatic model validation and binding
  - `[Route("api/[controller]")]`: Route is `/api/weather` (controller name without "Controller")
  - Inherits `ControllerBase` (no View support, API only)

```csharp
{
    private readonly IWeatherService _weatherService;

    public WeatherController(IWeatherService weatherService)
    {
        _weatherService = weatherService;
    }
```
- **Lines 9-15:** Constructor injection:
  - Receives `IWeatherService` from DI container
  - Stores in private readonly field
- **Link:** `IWeatherService` registered in Program.cs line 46.

```csharp
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var rawJson = await _weatherService.GetWeatherDataAsync(cancellationToken);

        if (rawJson is null)
            return NoContent();

        return Content(rawJson, "application/json");
    }
}
```
- **Lines 17-27:** GET endpoint at `/api/weather`:
  - `[HttpGet]`: Responds to GET requests
  - Accepts `CancellationToken` for request cancellation
  - Calls service to get weather data
  - Returns 204 No Content if null
  - Returns 200 with JSON content if data exists
- **Link:** Calls `WeatherService.GetWeatherDataAsync()`.

---

## 6. Services/IWeatherService.cs

**Purpose:** Interface defining the weather service contract.

```csharp
namespace SpacecraftWeatherApi.Services;

public interface IWeatherService
{
    Task<string?> GetWeatherDataAsync(CancellationToken cancellationToken = default);
}
```
- **Lines 1-6:** Interface with single method:
  - Returns nullable string (JSON or null)
  - Accepts optional cancellation token
  - Async operation
- **Link:** Implemented by `WeatherService`, used by `WeatherController`.

---

## 7. Services/WeatherService.cs

**Purpose:** Implementation of weather service with resilience, caching, and external API calls.

```csharp
using System.Text.Json;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Registry;
using SpacecraftWeatherApi.Configuration;
using SpacecraftWeatherApi.Repositories;
```
- **Lines 1-6:** Import namespaces for JSON parsing, options pattern, Polly, and project classes.

```csharp
namespace SpacecraftWeatherApi.Services;

public class WeatherService : IWeatherService
{
    private readonly HttpClient _httpClient;
    private readonly ResiliencePipelineProvider<string> _pipelineProvider;
    private readonly IWeatherRepository _repository;
    private readonly WeatherServiceOptions _options;
    private readonly ILogger<WeatherService> _logger;
```
- **Lines 8-16:** Class declaration with dependencies:
  - `HttpClient`: For HTTP requests (injected via typed client)
  - `ResiliencePipelineProvider`: Provides Polly pipelines
  - `IWeatherRepository`: For data persistence
  - `WeatherServiceOptions`: Configuration values
  - `ILogger`: For logging
- **Link:** All injected via constructor from DI container.

```csharp
    public WeatherService(
        HttpClient httpClient,
        ResiliencePipelineProvider<string> pipelineProvider,
        IWeatherRepository repository,
        IOptions<WeatherServiceOptions> options,
        ILogger<WeatherService> logger)
    {
        _httpClient = httpClient;
        _pipelineProvider = pipelineProvider;
        _repository = repository;
        _options = options.Value;
        _logger = logger;
    }
```
- **Lines 18-30:** Constructor stores all dependencies. Note `options.Value` unwraps the `IOptions` wrapper.

```csharp
    public async Task<string?> GetWeatherDataAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var pipeline = _pipelineProvider.GetPipeline<string>("weather");
```
- **Lines 32-36:** Method start:
  - Gets the "weather" pipeline registered in Program.cs
- **Link:** Pipeline configured in Program.cs lines 25-43.

```csharp
            var rawJson = await pipeline.ExecuteAsync(async ct =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, _options.Url);
```
- **Lines 38-40:** Executes HTTP call within resilience pipeline:
  - Creates GET request to configured URL

```csharp
                if (!string.IsNullOrEmpty(_options.AuthScheme) && !string.IsNullOrEmpty(_options.AuthToken))
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(_options.AuthScheme, _options.AuthToken);

                if (!string.IsNullOrEmpty(_options.ApiKey))
                    request.Headers.Add("X-Api-Key", _options.ApiKey);
```
- **Lines 42-46:** Adds authentication headers if configured:
  - Authorization header (Bearer/Basic)
  - X-Api-Key header
- **Link:** Values from `WeatherServiceOptions`.

```csharp
                var response = await _httpClient.SendAsync(request, ct);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync(ct);

                // Validate that the response is valid JSON
                using var doc = JsonDocument.Parse(content);

                return content;
            }, cancellationToken);
```
- **Lines 48-57:** HTTP execution:
  - Sends request
  - Throws on non-success status
  - Reads response as string
  - Validates JSON (throws if invalid)
  - Returns valid JSON string

```csharp
            await _repository.SaveAsync(rawJson, cancellationToken);
            _logger.LogInformation("Weather data fetched and persisted successfully");

            return rawJson;
        }
```
- **Lines 59-63:** On success:
  - Saves to database via repository
  - Logs success
  - Returns JSON to caller
- **Link:** Calls `WeatherRepository.SaveAsync()`.

```csharp
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch weather data from external service, falling back to cache");

            var cached = await _repository.GetLatestAsync(cancellationToken);
            if (cached is not null)
            {
                _logger.LogInformation("Returning cached weather data from {FetchedAt}", cached.FetchedAt);
                return cached.RawJson;
            }

            _logger.LogWarning("No cached weather data available");
            return null;
        }
    }
}
```
- **Lines 64-79:** Error handling (fallback to cache):
  - Catches any exception (after Polly retries exhausted)
  - Attempts to get cached data from database
  - Returns cached JSON if available
  - Returns null if no cache exists
- **Link:** Calls `WeatherRepository.GetLatestAsync()`.

---

## 8. Repositories/IWeatherRepository.cs

**Purpose:** Interface defining data persistence contract.

```csharp
using SpacecraftWeatherApi.Data.Entities;

namespace SpacecraftWeatherApi.Repositories;

public interface IWeatherRepository
{
    Task<WeatherRecord> SaveAsync(string rawJson, CancellationToken cancellationToken = default);
    Task<WeatherRecord?> GetLatestAsync(CancellationToken cancellationToken = default);
}
```
- **Lines 1-9:** Interface with two methods:
  - `SaveAsync`: Saves weather JSON, returns the created record
  - `GetLatestAsync`: Gets most recent cached record (nullable)
- **Link:** Uses `WeatherRecord` entity, implemented by `WeatherRepository`.

---

## 9. Repositories/WeatherRepository.cs

**Purpose:** EF Core implementation of weather data persistence.

```csharp
using Microsoft.EntityFrameworkCore;
using SpacecraftWeatherApi.Data;
using SpacecraftWeatherApi.Data.Entities;

namespace SpacecraftWeatherApi.Repositories;

public class WeatherRepository : IWeatherRepository
{
    private readonly WeatherDbContext _dbContext;

    public WeatherRepository(WeatherDbContext dbContext)
    {
        _dbContext = dbContext;
    }
```
- **Lines 1-14:** Class with `WeatherDbContext` dependency injection.
- **Link:** `WeatherDbContext` registered in Program.cs line 21-22.

```csharp
    public async Task<WeatherRecord> SaveAsync(string rawJson, CancellationToken cancellationToken = default)
    {
        var record = new WeatherRecord
        {
            RawJson = rawJson,
            FetchedAt = DateTime.UtcNow
        };

        _dbContext.WeatherRecords.Add(record);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return record;
    }
```
- **Lines 16-28:** Save method:
  - Creates new `WeatherRecord` with current UTC time
  - Adds to DbContext
  - Saves to database
  - Returns the saved record (with generated Id)

```csharp
    public async Task<WeatherRecord?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.WeatherRecords
            .OrderByDescending(r => r.FetchedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
```
- **Lines 30-36:** Get latest method:
  - Orders by `FetchedAt` descending (newest first)
  - Returns first or null if empty
- **Link:** Uses index on `FetchedAt` defined in `WeatherDbContext`.

---

## 10. Data/WeatherDbContext.cs

**Purpose:** EF Core database context for weather data.

```csharp
using Microsoft.EntityFrameworkCore;
using SpacecraftWeatherApi.Data.Entities;

namespace SpacecraftWeatherApi.Data;

public class WeatherDbContext : DbContext
{
    public WeatherDbContext(DbContextOptions<WeatherDbContext> options) : base(options)
    {
    }
```
- **Lines 1-10:** Context class:
  - Inherits `DbContext`
  - Constructor receives options (connection string, etc.)
- **Link:** Configured in Program.cs line 21-22.

```csharp
    public DbSet<WeatherRecord> WeatherRecords => Set<WeatherRecord>();
```
- **Line 12:** DbSet property for weather records table.
- **Link:** Used by `WeatherRepository` for database operations.

```csharp
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WeatherRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.FetchedAt);
        });
    }
}
```
- **Lines 14-22:** Entity configuration:
  - Sets `Id` as primary key
  - Creates index on `FetchedAt` for efficient "get latest" queries
- **Link:** Generates migration in Migrations folder.

---

## 11. Data/Entities/WeatherRecord.cs

**Purpose:** Entity model representing a cached weather record.

```csharp
namespace SpacecraftWeatherApi.Data.Entities;

public class WeatherRecord
{
    public long Id { get; set; }
    public string RawJson { get; set; } = string.Empty;
    public DateTime FetchedAt { get; set; }
}
```
- **Lines 1-8:** Simple entity class:
  - `Id`: Auto-increment primary key (long)
  - `RawJson`: The raw JSON response from external API
  - `FetchedAt`: UTC timestamp when data was fetched
- **Link:** Used by `WeatherDbContext`, `WeatherRepository`, and `IWeatherRepository`.

---

## File Linkage Summary

```
appsettings.json
    │
    ├──► Program.cs (reads configuration)
    │       │
    │       ├──► WeatherServiceOptions (binds "WeatherService" section)
    │       ├──► WeatherDbContext (uses "ConnectionStrings:WeatherDb")
    │       └──► Polly Pipeline (uses timeout/retry settings)
    │
    └──► WeatherService (uses IOptions<WeatherServiceOptions>)

Program.cs (DI Container)
    │
    ├──► IWeatherService ──► WeatherService
    ├──► IWeatherRepository ──► WeatherRepository
    └──► WeatherDbContext ──► SQLite Database

WeatherController
    │
    └──► IWeatherService.GetWeatherDataAsync()
            │
            ├──► Polly Pipeline (timeout + retry)
            ├──► HttpClient (external API call)
            └──► IWeatherRepository
                    │
                    ├──► SaveAsync() (on success)
                    └──► GetLatestAsync() (on failure, fallback)
                            │
                            └──► WeatherDbContext
                                    │
                                    └──► WeatherRecord (entity)
```

---

## Request Flow

1. **Client** sends GET request to `/api/weather`
2. **WeatherController.Get()** receives request
3. **WeatherService.GetWeatherDataAsync()** is called
4. **Polly Pipeline** wraps the HTTP call with timeout + retry
5. **HttpClient** makes request to external API with auth headers
6. **On Success:**
   - JSON validated
   - **WeatherRepository.SaveAsync()** stores in SQLite
   - JSON returned to controller
7. **On Failure (after retries):**
   - **WeatherRepository.GetLatestAsync()** retrieves cached data
   - Cached JSON returned (or null if no cache)
8. **Controller** returns 200 with JSON or 204 No Content
