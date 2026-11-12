using EmployeeManagement.Server.DTOs;

namespace EmployeeManagement.Server.Interfaces
{
    // Identiteti dhe flamuri i Administratorit vijne vetem nga JWT i verifikuar ne controller.
    public interface ITaskService
    {
        Task<List<TaskDto>> GetTasksAsync(string userId, bool isAdministrator);
        Task<TaskDto?> GetTaskByIdAsync(int id, string userId, bool isAdministrator);
        Task<TaskOperationResult> CreateTaskAsync(CreateTaskDto request, string userId, bool isAdministrator);
        Task<TaskOperationResult> UpdateTaskAsync(int id, UpdateTaskDto request, string userId, bool isAdministrator);
        Task<TaskOperationResult> CompleteTaskAsync(int id, string userId, bool isAdministrator);
        Task<TaskOperationResult> DeleteTaskAsync(int id);
    }
}
