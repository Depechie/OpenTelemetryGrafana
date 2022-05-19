using System.Threading.Tasks;
using WeatherAPI.Models;

namespace WeatherAPI.Services.Interfaces
{
    public interface ILocationService
	{
		Task<Location> GetLocation(double latitude, double longitude);
	}
}