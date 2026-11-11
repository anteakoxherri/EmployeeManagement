namespace EmployeeManagement.Server.DTOs
{
    // DTO demonstrues per endpoint-in e motit; nuk perfaqeson nje tabele EF Core.
    // Sherben vetem per te ruajtur shembullin e template-it ekzistues.
    public class WeatherForecast
    {
        public DateOnly Date { get; set; }

        public int TemperatureC { get; set; }

        // Property e llogaritur nga Celsius; nuk ka setter sepse nuk vendoset vecmas nga klienti.
        public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);

        public string? Summary { get; set; }
    }
}
