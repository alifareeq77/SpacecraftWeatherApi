using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Polly;
using Polly.Registry;
using SpacecraftWeatherApi.Configuration;
using SpacecraftWeatherApi.Data.Entities;
using SpacecraftWeatherApi.Repositories;
using SpacecraftWeatherApi.Services;
using Xunit;

namespace SpacecraftWeatherApi.Tests.Services;

public class WeatherServiceTests
{
    private readonly Mock<IWeatherRepository> _repoMock;
    private readonly IOptions<WeatherServiceOptions> _options;
    private readonly Mock<ILogger<WeatherService>> _loggerMock;

    public WeatherServiceTests()
    {
        _repoMock = new Mock<IWeatherRepository>();
        _options = Options.Create(new WeatherServiceOptions { Url = "https://example.com/weather" });
        _loggerMock = new Mock<ILogger<WeatherService>>();
    }

    private WeatherService CreateService(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        var stubHandler = new StubHttpHandler(handler);
        var httpClient = new HttpClient(stubHandler);
        var pipelineProvider = CreateStubPipelineProvider();

        return new WeatherService(httpClient, pipelineProvider, _repoMock.Object, _options, _loggerMock.Object);
    }

    [Fact]
    public async Task GetWeatherDataAsync_Success_ReturnsFetchedJsonAndPersists()
    {
        var json = """{"temperature":22,"unit":"C"}""";
        var service = CreateService((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            }));

        _repoMock
            .Setup(r => r.SaveAsync(json, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WeatherRecord { Id = 1, RawJson = json, FetchedAt = DateTime.UtcNow });

        var result = await service.GetWeatherDataAsync(CancellationToken.None);

        Assert.Equal(json, result);
        _repoMock.Verify(r => r.SaveAsync(json, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetWeatherDataAsync_HttpFails_ReturnsCachedData()
    {
        var cachedJson = """{"temperature":18}""";
        var service = CreateService((_, _) =>
            throw new HttpRequestException("Network error"));

        _repoMock
            .Setup(r => r.GetLatestAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WeatherRecord { Id = 1, RawJson = cachedJson, FetchedAt = DateTime.UtcNow });

        var result = await service.GetWeatherDataAsync(CancellationToken.None);

        Assert.Equal(cachedJson, result);
        _repoMock.Verify(r => r.GetLatestAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetWeatherDataAsync_HttpFails_NoCachedData_ReturnsNull()
    {
        var service = CreateService((_, _) =>
            throw new HttpRequestException("Network error"));

        _repoMock
            .Setup(r => r.GetLatestAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((WeatherRecord?)null);

        var result = await service.GetWeatherDataAsync(CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetWeatherDataAsync_InvalidJson_ReturnsCachedData()
    {
        var cachedJson = """{"temperature":15}""";
        var service = CreateService((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("not-valid-json", System.Text.Encoding.UTF8, "text/plain")
            }));

        _repoMock
            .Setup(r => r.GetLatestAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WeatherRecord { Id = 1, RawJson = cachedJson, FetchedAt = DateTime.UtcNow });

        var result = await service.GetWeatherDataAsync(CancellationToken.None);

        Assert.Equal(cachedJson, result);
    }

    private static ResiliencePipelineProvider<string> CreateStubPipelineProvider()
    {
        var services = new ServiceCollection();
        services.AddResiliencePipeline<string, string>("weather", _ => { });
        var sp = services.BuildServiceProvider();
        return sp.GetRequiredService<ResiliencePipelineProvider<string>>();
    }

    private class StubHttpHandler : DelegatingHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public StubHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }
    }
}
