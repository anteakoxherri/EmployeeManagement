
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeManagement.Server.Controllers
{
    // Endpoint i thjeshte per te provuar autorizimin sipas rolit; nuk lexon ose ndryshon databazen.
    // Na ndihmon te dallojme verifikimin e token-it nga leja per nje veprim te caktuar.
    [ApiController]
    [Route("api/test")]
    public class TestController : ControllerBase
    {
        // Nuk merr parametra. Kthen 200 me nje mesazh vetem pas kontrollit te rolit Administrator.
        // Middleware i autentikimit/autorizimit e ndalon kerkesen perpara metodes kur mungon e drejta.
        [HttpGet("admin")]
        [Authorize(Roles = "Administrator")]
        public IActionResult AdminOnly()
        {
            return Ok("Akses i lejuar per Administrator!");
        }
    }
}
