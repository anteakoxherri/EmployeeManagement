using EmployeeManagement.Server.DTOs;

namespace EmployeeManagement.Server.Interfaces
{
    // Kontrata e service-it demonstrues te motit, e injektuar ne WeatherForecastController.
    // Ruan ndarjen controller/service edhe per endpoint-in e template-it.
    public interface IWeatherForecastService
    {
        // Pa parametra; kthen nje koleksion parashikimesh shembull, pa lexim databaze.
        IEnumerable<WeatherForecast> GetForecasts();
    }
}
