using EmployeeManagement.Server.DTOs;
using EmployeeManagement.Server.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeManagement.Server.Controllers
{
    // Controller demonstrues i template-it, i ruajtur per te mos ndryshuar endpoint-in ekzistues.
    // Gjenerimi i te dhenave eshte ne service; ky controller merret vetem me kerkesen HTTP.
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private readonly IWeatherForecastService _weatherForecastService;

        // Merr implementimin permes DI, njesoj si controllers e perdoruesve dhe punonjesve.
        public WeatherForecastController(IWeatherForecastService weatherForecastService)
        {
            _weatherForecastService = weatherForecastService;
        }

        // GET /WeatherForecast nuk merr parametra dhe kthen pese parashikime shembull, pa databaze.
        [HttpGet(Name = "GetWeatherForecast")]
        public IEnumerable<WeatherForecast> Get()
        {
            return _weatherForecastService.GetForecasts();
        }
    }
}
