using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using WeatherAPI.Models;
using WeatherAPI.Services;
using WeatherAPI.Services.Interfaces;

namespace WeatherAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<WeatherForecastController> _logger;
        private readonly ILocationService _locationService;

        public WeatherForecastController(ILogger<WeatherForecastController> logger, ILocationService locationService)
        {
            _logger = logger;
            _locationService = locationService;
        }

        [HttpGet]
        public async Task<LocationWeatherForecast> Get(double? latitude, double? longitude)
        {
            string locationName = "Seattle";

            if (latitude is not null && longitude is not null)
            {
                // https://localhost:5501/Location?latitude=51.260197&longitude=4.402771
                var location = await _locationService.GetLocation(latitude.Value, longitude.Value);
                locationName = location?.Name;
            }

            var rng = new Random();
            var weatherForecasts = Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = rng.Next(-20, 55),
                Summary = Summaries[rng.Next(Summaries.Length)]
            });

            return new LocationWeatherForecast()
            {
                Location = locationName,
                WeatherForecasts = weatherForecasts
            };
        }
    }
}