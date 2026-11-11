using EmployeeManagement.Server.Data;
using EmployeeManagement.Server.DTOs;
using EmployeeManagement.Server.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagement.Server.Services
{
    // Mban query-n e punonjesve. EmployeesController e therrret permes IEmployeeService,
    // kurse ApplicationDbContext e perkthen LINQ-ne ne SQL per tabelat e Identity.
    public class EmployeeService : IEmployeeService
    {
        private readonly ApplicationDbContext _context;

        // DbContext vjen nga DI; service nuk hap lidhje manuale dhe nuk mban connection string.
        public EmployeeService(ApplicationDbContext context)
        {
            _context = context;
        }

        // Nuk merr parametra. Kthen vetem perdoruesit qe jane anetare te rolit Employee.
        // Nje Administrator pa kete rol nuk perfshihet; perdoruesi me te dy rolet perfshihet.
        public async Task<List<EmployeeViewModel>> GetEmployeesAsync()
        {
            // Identity ruan emrin e normalizuar me shkronja te medha per krahasim te qendrueshem.
            var employeeRoleIds = _context.Roles
                .Where(role => role.NormalizedName == "EMPLOYEE")
                .Select(role => role.Id);

            return await _context.Users
                .AsNoTracking()
                // Any kontrollon anetaresimin pa prodhuar rreshta te perseritur per te njejtin perdorues.
                .Where(user => _context.UserRoles.Any(userRole =>
                    userRole.UserId == user.Id &&
                    employeeRoleIds.Contains(userRole.RoleId)))
                // Projektimi ne DTO lexon vetem fushat e nevojshme dhe nuk dergon entitetin Identity ne klient.
                .Select(user => new EmployeeViewModel
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    Email = user.Email
                })
                .ToListAsync();
        }
    }
}
