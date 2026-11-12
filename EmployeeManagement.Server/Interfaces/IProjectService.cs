using EmployeeManagement.Server.DTOs;

namespace EmployeeManagement.Server.Interfaces
{
    // Kontrata e projekteve/anetaresimeve; identiteti dhe roli vijne nga JWT i verifikuar.
    public interface IProjectService
    {
        Task<List<ProjectDto>> GetProjectsAsync(string userId, bool isAdministrator);

        // null do te thote qe projekti nuk ekziston ose perdoruesi nuk ka akses.
        Task<ProjectDto?> GetProjectByIdAsync(int id, string userId, bool isAdministrator);

        Task<ProjectOperationResult> CreateProjectAsync(ProjectRequestDto request);
        Task<ProjectOperationResult> UpdateProjectAsync(int id, ProjectRequestDto request);
        Task<ProjectOperationResult> DeleteProjectAsync(int id);
        Task<ProjectOperationResult> AddEmployeeAsync(int projectId, string userId);
        Task<ProjectOperationResult> RemoveEmployeeAsync(int projectId, string userId);

        // null kur projekti nuk ekziston/nuk lejohet; lista bosh kur projekti nuk ka anetare.
        Task<List<ProjectEmployeeDto>?> GetProjectEmployeesAsync(int projectId, string userId, bool isAdministrator);
    }
}
