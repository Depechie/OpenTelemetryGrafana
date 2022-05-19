using System;
using System.Collections.Generic;

namespace WeatherAPI.Models
{
    public class LocationWeatherForecast
    {
        public string Location { get; set; }

        public IEnumerable<WeatherForecast> WeatherForecasts { get; set; }
    }

    public class WeatherForecast
    {
        public DateTime Date { get; set; }

        public int TemperatureC { get; set; }

        public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);

        public string Summary { get; set; }
    }
}
