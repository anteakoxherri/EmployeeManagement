using EmployeeManagement.Server.DTOs;
using EmployeeManagement.Server.Interfaces;

namespace EmployeeManagement.Server.Services
{
    // Service demonstrues i template-it: prodhon te dhena shembull dhe nuk perdor SQL Server.
    // Ka interface qe controller-i te ndjeke te njejten menyre komunikimi si pjesa tjeter e API-se.
    public class WeatherForecastService : IWeatherForecastService
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        // Pa parametra; kthen pese objekte WeatherForecast me data te ardhshme dhe temperatura te rastesishme.
        public IEnumerable<WeatherForecast> GetForecasts()
        {
            // Kjo LINQ ekzekutohet ne memorie, ndryshe nga query-t EF Core qe perkthehen ne SQL.
            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
        }
    }
}
