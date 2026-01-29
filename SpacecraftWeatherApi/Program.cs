using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;
using Polly.Timeout;
using SpacecraftWeatherApi.Configuration;
using SpacecraftWeatherApi.Data;
using SpacecraftWeatherApi.Repositories;
using SpacecraftWeatherApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<WeatherServiceOptions>(
    builder.Configuration.GetSection(WeatherServiceOptions.SectionName));

var weatherOptions = builder.Configuration
    .GetSection(WeatherServiceOptions.SectionName)
    .Get<WeatherServiceOptions>() ?? new WeatherServiceOptions();

// EF Core + SQLite
builder.Services.AddDbContext<WeatherDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("WeatherDb")));

// Polly resilience pipeline
builder.Services.AddResiliencePipeline<string, string>("weather", pipelineBuilder =>
{
    pipelineBuilder
        .AddTimeout(new TimeoutStrategyOptions
        {
            Timeout = TimeSpan.FromMilliseconds(weatherOptions.TimeoutMs)
        })
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

// HttpClient + WeatherService (typed client handles IWeatherService registration)
builder.Services.AddHttpClient<IWeatherService, WeatherService>();

// Repositories
builder.Services.AddScoped<IWeatherRepository, WeatherRepository>();

// Controllers & OpenAPI
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Auto-migrate database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<WeatherDbContext>();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Spacecraft Weather API v1");
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
