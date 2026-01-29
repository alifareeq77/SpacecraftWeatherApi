using Microsoft.AspNetCore.Mvc;
using Moq;
using SpacecraftWeatherApi.Controllers;
using SpacecraftWeatherApi.Services;
using Xunit;

namespace SpacecraftWeatherApi.Tests.Controllers;

public class WeatherControllerTests
{
    private readonly Mock<IWeatherService> _serviceMock;
    private readonly WeatherController _controller;

    public WeatherControllerTests()
    {
        _serviceMock = new Mock<IWeatherService>();
        _controller = new WeatherController(_serviceMock.Object);
    }

    [Fact]
    public async Task Get_WhenServiceReturnsJson_Returns200WithJson()
    {
        var json = """{"temperature":22}""";
        _serviceMock
            .Setup(s => s.GetWeatherDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        var result = await _controller.Get(CancellationToken.None);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(json, content.Content);
        Assert.Equal("application/json", content.ContentType);
    }

    [Fact]
    public async Task Get_WhenServiceReturnsNull_Returns204()
    {
        _serviceMock
            .Setup(s => s.GetWeatherDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await _controller.Get(CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Get_PassesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _serviceMock
            .Setup(s => s.GetWeatherDataAsync(token))
            .ReturnsAsync("{}");

        await _controller.Get(token);

        _serviceMock.Verify(s => s.GetWeatherDataAsync(token), Times.Once);
    }
}
