
using EmployeeManagement.Server.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeManagement.Server.Controllers
{
    // Ekspozon vetem listimin e punonjesve. Ndryshe nga /api/users, rezultati filtrohet sipas rolit Employee.
    // ApiController lidh kerkesen me metoden, ndersa Authorize kerkon token te vlefshem te Administratorit.
    [ApiController]
    [Route("api/employees")]
    [Authorize(Roles = "Administrator")]
    public class EmployeesController : ControllerBase
    {
        private readonly IEmployeeService _employeeService;

        // Marrim sherbimin per punonjesit permes Dependency Injection
        public EmployeesController(IEmployeeService employeeService)
        {
            _employeeService = employeeService;
        }

        // GET: api/employees
        // Nuk merr parametra; pret service-in dhe kthen 200 me listen, edhe kur lista eshte bosh.
        // Filtrimi eshte ne service qe logjika e databazes te mos perseritet ne controller.
        [HttpGet]
        public async Task<IActionResult> GetEmployees()
        {
            var employees = await _employeeService.GetEmployeesAsync();

            return Ok(employees);
        }
    }
}
