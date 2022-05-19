using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using WeatherAPI.Models;
using WeatherAPI.Services.Interfaces;

namespace WeatherAPI.Services
{
	public class LocationService : ILocationService
	{
        private HttpClient _httpClient;
        private JsonSerializerOptions _options;

        public LocationService(HttpClient httpClient)
		{
			_httpClient = httpClient;

            _options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<Location> GetLocation(double latitude, double longitude)
        {
            // https://localhost:5501/Location?latitude=51.260197&longitude=4.402771
            return JsonSerializer.Deserialize<Location>(await _httpClient.GetStringAsync($"https://localhost:5501/Location?latitude={latitude.ToString(CultureInfo.InvariantCulture)}&longitude={longitude.ToString(CultureInfo.InvariantCulture)}"), _options);
        }
    }
}

