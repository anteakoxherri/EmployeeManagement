using EmployeeManagement.Server.Data;
using EmployeeManagement.Server.DTOs;
using EmployeeManagement.Server.Interfaces;
using Microsoft.EntityFrameworkCore;
using EmployeeManagement.Models;
using Microsoft.Data.SqlClient;
using System.Data;
using EmployeeManagement.Server.Events;

namespace EmployeeManagement.Server.Services
{
    // Mban query-t dhe filtrimin e projekteve; DbContext merret permes Dependency Injection.
    public class ProjectService : IProjectService
    {
        private readonly ApplicationDbContext _context;
        private readonly IRabbitMqPublisher _publisher;

        public ProjectService(ApplicationDbContext context, IRabbitMqPublisher publisher)
        {
            _context = context;
            _publisher = publisher;
        }

        // Administratori lexon te gjitha projektet; Employee merr vetem anetaresimet e veta.
        // Lista bosh eshte rezultat i vlefshem kur nuk ka projekte te aksesueshme.
        public Task<List<ProjectDto>> GetProjectsAsync(string userId, bool isAdministrator)
        {
            return QueryAccessibleProjects(userId, isAdministrator)
                .OrderBy(project => project.Id)
                .ToListAsync();
        }

        // Filtrimi i aksesit zbatohet para kerkimit sipas ID-se, ndaj nuk zbulohet nje projekt i palejuar.
        public Task<ProjectDto?> GetProjectByIdAsync(int id, string userId, bool isAdministrator)
        {
            return QueryAccessibleProjects(userId, isAdministrator)
                .SingleOrDefaultAsync(project => project.Id == id);
        }

        // Krijon projektin nga fushat e lejuara, pa pranuar navigime ose ID nga klienti.
        public Task<ProjectOperationResult> CreateProjectAsync(ProjectRequestDto request)
            => ExecuteChangeAsync(async events =>
            {
                var project = new Project { Name = request.Name, Description = request.Description };
                _context.Projects.Add(project);
                await _context.SaveChangesAsync();
                events.Add(new(BusinessEventType.ProjectCreated, project.Id.ToString(), ProjectId: project.Id));
                return new ProjectOperationResult { Project = ToDto(project) };
            });

        // PUT ruan ID-ne/daten e krijimit dhe ndryshon vetem emrin e pershkrimin.
        public Task<ProjectOperationResult> UpdateProjectAsync(int id, ProjectRequestDto request)
            => ExecuteChangeAsync(async events =>
            {
                var project = await _context.Projects.SingleOrDefaultAsync(project => project.Id == id);
                if (project == null)
                    return Failure(ProjectOperationStatus.NotFound, "Project not found.");

                project.Name = request.Name;
                project.Description = request.Description;
                await _context.SaveChangesAsync();
                events.Add(new(BusinessEventType.ProjectUpdated, project.Id.ToString(), ProjectId: project.Id));
                return new ProjectOperationResult { Project = ToDto(project) };
            });

        // Nuk lejohet fshirja nese ekziston qofte edhe nje detyre e paperfunduar.
        public Task<ProjectOperationResult> DeleteProjectAsync(int id)
            => ExecuteChangeAsync(async events =>
            {
                var project = await _context.Projects.SingleOrDefaultAsync(project => project.Id == id);
                if (project == null)
                    return Failure(ProjectOperationStatus.NotFound, "Project not found.");
                if (await _context.EmployeeTasks.AnyAsync(task => task.ProjectId == id && !task.IsCompleted))
                    return Failure(ProjectOperationStatus.Conflict, "Projects with incomplete tasks cannot be deleted.");

                // FK ka Restrict: hiqen shprehimisht detyrat e perfunduara para projektit.
                // Transaksioni i anulon te gjitha fshirjet nese ndonje hap deshton.
                await _context.EmployeeTasks.Where(task => task.ProjectId == id).ExecuteDeleteAsync();
                _context.Projects.Remove(project);
                // Cascade ekzistues heq anetaresimet e ketij projekti, por nuk fshin perdoruesit.
                await _context.SaveChangesAsync();
                // Ky event pershkruan fshirjen e projektit, perfshire pastrimin ekzistues te lidhjeve.
                events.Add(new(BusinessEventType.ProjectDeleted, id.ToString(), ProjectId: id));
                return new ProjectOperationResult();
            });

        // Shton vetem nje perdorues ekzistues qe ka rolin Employee, pa ndryshuar rolet e tij.
        public Task<ProjectOperationResult> AddEmployeeAsync(int projectId, string userId)
            => ExecuteChangeAsync(async events =>
            {
                if (!await _context.Projects.AnyAsync(project => project.Id == projectId))
                    return Failure(ProjectOperationStatus.NotFound, "Project not found.");
                if (!await _context.Users.AnyAsync(user => user.Id == userId))
                    return Failure(ProjectOperationStatus.NotFound, "User not found.");

                var isEmployee = await (from membership in _context.UserRoles
                                        join role in _context.Roles on membership.RoleId equals role.Id
                                        where membership.UserId == userId && role.NormalizedName == "EMPLOYEE"
                                        select membership.UserId).AnyAsync();
                if (!isEmployee)
                    return Failure(ProjectOperationStatus.Invalid, "The user must have the Employee role.");
                if (await _context.ProjectEmployees.AnyAsync(member => member.ProjectId == projectId && member.UserId == userId))
                    return Failure(ProjectOperationStatus.Conflict, "The employee already belongs to this project.");

                _context.ProjectEmployees.Add(new ProjectEmployee { ProjectId = projectId, UserId = userId });
                await _context.SaveChangesAsync();
                events.Add(new(BusinessEventType.EmployeeAddedToProject, projectId.ToString(), ProjectId: projectId, UserId: userId));
                return new ProjectOperationResult();
            });

        // Heqja bllokohet kur ka detyra te caktuara, duke ruajtur caktimet dhe historikun.
        public Task<ProjectOperationResult> RemoveEmployeeAsync(int projectId, string userId)
            => ExecuteChangeAsync(async events =>
            {
                if (!await _context.Projects.AnyAsync(project => project.Id == projectId))
                    return Failure(ProjectOperationStatus.NotFound, "Project not found.");
                var membership = await _context.ProjectEmployees.SingleOrDefaultAsync(member => member.ProjectId == projectId && member.UserId == userId);
                if (membership == null)
                    return Failure(ProjectOperationStatus.NotFound, "Project membership not found.");
                if (await _context.EmployeeTasks.AnyAsync(task => task.ProjectId == projectId && task.AssignedToUserId == userId))
                    return Failure(ProjectOperationStatus.Conflict, "Reassign or remove the employee's assigned tasks before removing project membership.");

                _context.ProjectEmployees.Remove(membership);
                await _context.SaveChangesAsync();
                events.Add(new(BusinessEventType.EmployeeRemovedFromProject, projectId.ToString(), ProjectId: projectId, UserId: userId));
                return new ProjectOperationResult();
            });

        // Kontrolli i aksesit dhe leximi i anetareve behen ne te njejtin query.
        public async Task<List<ProjectEmployeeDto>?> GetProjectEmployeesAsync(int projectId, string userId, bool isAdministrator)
        {
            var result = await AccessibleProjects(userId, isAdministrator)
                .Where(project => project.Id == projectId)
                .Select(project => new
                {
                    Employees = project.ProjectEmployees.OrderBy(member => member.UserId)
                        .Select(member => new ProjectEmployeeDto
                        {
                            Id = member.UserId,
                            FirstName = member.User.FirstName,
                            LastName = member.User.LastName,
                            Email = member.User.Email
                        }).ToList()
                }).SingleOrDefaultAsync();
            return result?.Employees;
        }

        // Transaksioni Serializable mbron kontrollet nga detyra/anetaresime qe ndryshojne njekohesisht.
        // Vetem rezultati Success kryen Commit; ne raste te tjera Dispose anulon shkrimet.
        private async Task<ProjectOperationResult> ExecuteChangeAsync(Func<List<BusinessEvent>, Task<ProjectOperationResult>> change)
        {
            try
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                var events = new List<BusinessEvent>();
                var result = await change(events);
                if (result.Status == ProjectOperationStatus.Success)
                {
                    await transaction.CommitAsync();
                    // SaveChanges nuk mjafton: vetem Commit konfirmon qe ndryshimet mbeten ne DB.
                    foreach (var businessEvent in events) _publisher.Publish(businessEvent);
                }
                return result;
            }
            catch (DbUpdateConcurrencyException)
            {
                return Failure(ProjectOperationStatus.Conflict, "The project data changed. Reload and try again.");
            }
            catch (Exception exception) when (IsSqlConflict(exception))
            {
                return Failure(ProjectOperationStatus.Conflict, "A duplicate membership or concurrent related change prevents this operation. Reload and try again.");
            }
        }

        // EF mund ta mbeshtjelle gabimin SQL ne disa nivele; nuk zbulojme detaje SQL ne API.
        private static bool IsSqlConflict(Exception exception)
        {
            for (Exception? cause = exception; cause != null; cause = cause.InnerException)
                if (cause is SqlException { Number: 2601 or 2627 or 547 or 1205 })
                    return true;
            return false;
        }

        private static ProjectOperationResult Failure(ProjectOperationStatus status, string error)
            => new() { Status = status, Error = error };

        private static ProjectDto ToDto(Project project)
            => new() { Id = project.Id, Name = project.Name, Description = project.Description, CreatedAt = project.CreatedAt };

        // Te gjitha leximet ndajne te njejtin kontroll aksesi, i cili ekzekutohet ne SQL.
        private IQueryable<Project> AccessibleProjects(string userId, bool isAdministrator)
        {
            var projects = _context.Projects.AsNoTracking();

            if (!isAdministrator)
            {
                // Any perdor lidhjen ekzistuese ProjectEmployee pa ngarkuar perdoruesit ose task-et.
                projects = projects.Where(project => project.ProjectEmployees.Any(membership => membership.UserId == userId));
            }

            return projects;
        }

        private IQueryable<ProjectDto> QueryAccessibleProjects(string userId, bool isAdministrator)
        {
            return AccessibleProjects(userId, isAdministrator).Select(project => new ProjectDto
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                CreatedAt = project.CreatedAt
            });
        }
    }
}
