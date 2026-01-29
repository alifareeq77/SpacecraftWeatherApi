using Microsoft.AspNetCore.Mvc;
using SpacecraftWeatherApi.Services;

namespace SpacecraftWeatherApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WeatherController : ControllerBase
{
    private readonly IWeatherService _weatherService;

    public WeatherController(IWeatherService weatherService)
    {
        _weatherService = weatherService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var rawJson = await _weatherService.GetWeatherDataAsync(cancellationToken);

        if (rawJson is null)
            return NoContent();

        return Content(rawJson, "application/json");
    }
}
