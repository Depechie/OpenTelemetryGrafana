using LocationAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LocationAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class LocationController : ControllerBase
    {
        private readonly ILogger<LocationController> _logger;

        public LocationController(ILogger<LocationController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public Location Get(double latitude, double longitude)
        {
            return new Location()
            {
                Name = "Antwerp",
                Latitude = latitude,
                Longitude = longitude
            };
        }
    }
}