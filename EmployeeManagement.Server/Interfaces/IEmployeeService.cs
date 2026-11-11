using EmployeeManagement.Server.DTOs;

namespace EmployeeManagement.Server.Interfaces
{
    // Kontrata e listimit te punonjesve; ndan controller-in nga implementimi konkret EmployeeService.
    // Kjo lejon edhe zevendesimin e service-it me nje implementim provues gjate testeve.
    public interface IEmployeeService
    {
        // Pa parametra; kthen listen e perdoruesve me rolin Employee, pa te dhena te brendshme te Identity.
        Task<List<EmployeeViewModel>> GetEmployeesAsync();
    }
}
