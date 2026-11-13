using System.Data;
using EmployeeManagement.Models;
using EmployeeManagement.Server.Data;
using EmployeeManagement.Server.DTOs;
using EmployeeManagement.Server.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using EmployeeManagement.Server.Events;

namespace EmployeeManagement.Server.Services
{
    // Zbaton lejet dhe query-t e detyrave ne backend, pavaresisht klientit qe dergon kerkesen.
    public class TaskService : ITaskService
    {
        private readonly ApplicationDbContext _context;
        private readonly IRabbitMqPublisher _publisher;

        public TaskService(ApplicationDbContext context, IRabbitMqPublisher publisher)
        {
            _context = context;
            _publisher = publisher;
        }

        // Administratori sheh gjithcka; Employee sheh cdo detyre te projekteve ku eshte anetar.
        public Task<List<TaskDto>> GetTasksAsync(string userId, bool isAdministrator)
            => QueryTaskDtos(userId, isAdministrator).OrderBy(task => task.Id).ToListAsync();

        // E njejta 404 perdoret per detyren qe mungon dhe per projektin e palejuar.
        public Task<TaskDto?> GetTaskByIdAsync(int id, string userId, bool isAdministrator)
            => QueryTaskDtos(userId, isAdministrator).SingleOrDefaultAsync(task => task.Id == id);

        public Task<TaskOperationResult> CreateTaskAsync(CreateTaskDto request, string userId, bool isAdministrator)
            => ExecuteChangeAsync(async events =>
            {
                var validation = await ValidateProjectAndAssigneeAsync(request, userId, isAdministrator);
                if (validation != null)
                    return validation;

                var task = new EmployeeTask
                {
                    Title = request.Title,
                    Description = request.Description,
                    ProjectId = request.ProjectId,
                    AssignedToUserId = request.AssignedToUserId,
                    // Nuk pranojme krijues, data ose gjendje perfundimi nga body i krijimit.
                    CreatedByUserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    IsCompleted = false,
                    CompletedAt = null
                };
                _context.EmployeeTasks.Add(task);
                await _context.SaveChangesAsync();
                events.Add(TaskEvent(BusinessEventType.TaskCreated, task));
                if (task.AssignedToUserId != null)
                    events.Add(TaskEvent(BusinessEventType.TaskAssigned, task));
                return new TaskOperationResult { Task = ToDto(task) };
            });

        public Task<TaskOperationResult> UpdateTaskAsync(int id, UpdateTaskDto request, string userId, bool isAdministrator)
            => ExecuteChangeAsync(async events =>
            {
                var task = await AccessibleTasks(userId, isAdministrator).SingleOrDefaultAsync(task => task.Id == id);
                if (task == null)
                    return Failure(TaskOperationStatus.NotFound, "Task not found.");

                // Kontrollojme caktimin ekzistues PARA ndryshimeve; klienti nuk mund ta marre
                // nje detyre tjeter duke derguar ID-ne e vet ne AssignedToUserId.
                if (!isAdministrator && task.AssignedToUserId != userId)
                    return Failure(TaskOperationStatus.Forbidden, "Employees can only modify tasks assigned to them.");

                var validation = await ValidateProjectAndAssigneeAsync(request, userId, isAdministrator);
                if (validation != null)
                    return validation;

                var previousAssignee = task.AssignedToUserId;
                var wasCompleted = task.IsCompleted;
                task.Title = request.Title;
                task.Description = request.Description;
                task.ProjectId = request.ProjectId;
                task.AssignedToUserId = request.AssignedToUserId;
                if (request.IsCompleted.HasValue)
                    SetCompletion(task, request.IsCompleted.Value);
                // CreatedAt dhe CreatedByUserId ruhen edhe kur ndryshon projekti ose caktimi.
                await _context.SaveChangesAsync();
                events.Add(TaskEvent(BusinessEventType.TaskUpdated, task));
                if (task.AssignedToUserId != null && task.AssignedToUserId != previousAssignee)
                    events.Add(TaskEvent(BusinessEventType.TaskAssigned, task));
                if (!wasCompleted && task.IsCompleted)
                    events.Add(TaskEvent(BusinessEventType.TaskCompleted, task));
                return new TaskOperationResult { Task = ToDto(task) };
            });

        // Perfundohet cdo detyre nga Administratori, ose vetem detyra e vet nga Employee.
        public Task<TaskOperationResult> CompleteTaskAsync(int id, string userId, bool isAdministrator)
            => ExecuteChangeAsync(async events =>
            {
                var task = await AccessibleTasks(userId, isAdministrator).SingleOrDefaultAsync(task => task.Id == id);
                if (task == null)
                    return Failure(TaskOperationStatus.NotFound, "Task not found.");
                if (!isAdministrator && task.AssignedToUserId != userId)
                    return Failure(TaskOperationStatus.Forbidden, "Employees can only complete tasks assigned to them.");

                var wasCompleted = task.IsCompleted;
                SetCompletion(task, true);
                await _context.SaveChangesAsync();
                // PATCH i perseritur nuk krijon nje perfundim te ri kur detyra eshte tashme e perfunduar.
                if (!wasCompleted) events.Add(TaskEvent(BusinessEventType.TaskCompleted, task));
                return new TaskOperationResult { Task = ToDto(task) };
            });

        // Vetem endpoint-i Administrator e therrret kete veprim; hiqet vetem detyra e zgjedhur.
        public Task<TaskOperationResult> DeleteTaskAsync(int id)
            => ExecuteChangeAsync(async events =>
            {
                var task = await _context.EmployeeTasks.SingleOrDefaultAsync(task => task.Id == id);
                if (task == null)
                    return Failure(TaskOperationStatus.NotFound, "Task not found.");
                _context.EmployeeTasks.Remove(task);
                await _context.SaveChangesAsync();
                events.Add(TaskEvent(BusinessEventType.TaskDeleted, task));
                return new TaskOperationResult();
            });

        // Nese ndryshon projekti ne PUT, Employee duhet te jete anetar edhe i projektit te ri.
        private async Task<TaskOperationResult?> ValidateProjectAndAssigneeAsync(CreateTaskDto request, string userId, bool isAdministrator)
        {
            var projectAccessible = await _context.Projects.AnyAsync(project => project.Id == request.ProjectId &&
                (isAdministrator || project.ProjectEmployees.Any(member => member.UserId == userId)));
            if (!projectAccessible)
                return Failure(TaskOperationStatus.NotFound, "Project not found.");

            if (request.AssignedToUserId != null)
            {
                var validAssignee = await (from member in _context.ProjectEmployees
                                           join userRole in _context.UserRoles on member.UserId equals userRole.UserId
                                           join role in _context.Roles on userRole.RoleId equals role.Id
                                           where member.ProjectId == request.ProjectId && member.UserId == request.AssignedToUserId && role.NormalizedName == "EMPLOYEE"
                                           select member.UserId).AnyAsync();
                if (!validAssignee)
                    return Failure(TaskOperationStatus.Invalid, "The assignee must be an Employee who belongs to the task's project.");
            }
            return null;
        }

        // Thirrjet e perseritura te perfundimit ruajne daten e pare. Rihapja pastron CompletedAt.
        private static void SetCompletion(EmployeeTask task, bool completed)
        {
            task.IsCompleted = completed;
            task.CompletedAt = completed ? task.CompletedAt ?? DateTime.UtcNow : null;
        }

        private IQueryable<EmployeeTask> AccessibleTasks(string userId, bool isAdministrator)
        {
            var tasks = _context.EmployeeTasks.AsQueryable();
            if (!isAdministrator)
                tasks = tasks.Where(task => task.Project.ProjectEmployees.Any(member => member.UserId == userId));
            return tasks;
        }

        private IQueryable<TaskDto> QueryTaskDtos(string userId, bool isAdministrator)
            => AccessibleTasks(userId, isAdministrator).AsNoTracking().Select(task => new TaskDto
            {
                Id = task.Id, Title = task.Title, Description = task.Description,
                ProjectId = task.ProjectId, AssignedToUserId = task.AssignedToUserId,
                CreatedByUserId = task.CreatedByUserId, IsCompleted = task.IsCompleted,
                CreatedAt = task.CreatedAt, CompletedAt = task.CompletedAt
            });

        // Transaksioni ruan vlefshmerine e anetaresimit/caktimit gjate shkrimit dhe mbron
        // kontrollin ekzistues te fshirjes se projektit nga krijimi/rihapja e detyrave njekohesisht.
        private async Task<TaskOperationResult> ExecuteChangeAsync(Func<List<BusinessEvent>, Task<TaskOperationResult>> change)
        {
            try
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                var events = new List<BusinessEvent>();
                var result = await change(events);
                if (result.Status == TaskOperationStatus.Success)
                {
                    await transaction.CommitAsync();
                    // Eventet e veprimeve te refuzuara ose transaksioneve te anuluara nuk dergohen.
                    foreach (var businessEvent in events) _publisher.Publish(businessEvent);
                }
                return result;
            }
            catch (DbUpdateConcurrencyException)
            {
                return Failure(TaskOperationStatus.Conflict, "The task data changed. Reload and try again.");
            }
            catch (Exception exception) when (IsSqlConflict(exception))
            {
                return Failure(TaskOperationStatus.Conflict, "Related task data changed. Reload and try again.");
            }
        }

        // EF mund ta mbeshtjelle SqlException; konfliktet kthehen pa ekspozuar detaje SQL.
        private static bool IsSqlConflict(Exception exception)
        {
            for (Exception? cause = exception; cause != null; cause = cause.InnerException)
                if (cause is SqlException { Number: 547 or 1205 })
                    return true;
            return false;
        }

        private static TaskOperationResult Failure(TaskOperationStatus status, string error)
            => new() { Status = status, Error = error };

        private static BusinessEvent TaskEvent(BusinessEventType type, EmployeeTask task)
            => new(type, task.Id.ToString(), ProjectId: task.ProjectId, TaskId: task.Id,
                UserId: task.CreatedByUserId, AssignedToUserId: task.AssignedToUserId);

        private static TaskDto ToDto(EmployeeTask task)
            => new()
            {
                Id = task.Id, Title = task.Title, Description = task.Description,
                ProjectId = task.ProjectId, AssignedToUserId = task.AssignedToUserId,
                CreatedByUserId = task.CreatedByUserId, IsCompleted = task.IsCompleted,
                CreatedAt = task.CreatedAt, CompletedAt = task.CompletedAt
            };
    }
}
