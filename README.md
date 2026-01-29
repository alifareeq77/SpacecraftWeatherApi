# Spacecraft Weather API

An ASP.NET Core 9.0 Web API for weather forecasting.

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

## Getting Started

### Build

```bash
dotnet build SpacecraftWeatherApi/SpacecraftWeatherApi.csproj
```

### Run

```bash
dotnet run --project SpacecraftWeatherApi/SpacecraftWeatherApi.csproj
```

Or with a specific launch profile:

```bash
# HTTP only
dotnet run --project SpacecraftWeatherApi/SpacecraftWeatherApi.csproj --launch-profile http

# HTTPS (default)
dotnet run --project SpacecraftWeatherApi/SpacecraftWeatherApi.csproj --launch-profile https
```

### Local URLs

| Profile | URL |
|---------|-----|
| HTTP | http://localhost:5003 |
| HTTPS | https://localhost:7059 |

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/weather` | Get weather data |

## API Documentation

OpenAPI specification is available at `/openapi/v1.json` when running in Development environment.

Swagger UI is available at `/swagger`.

## Running Tests

```bash
dotnet test
```

## Technology Stack

- ASP.NET Core 9.0
- Entity Framework Core with SQLite
- Polly (resilience policies)
- Swashbuckle (Swagger/OpenAPI)
