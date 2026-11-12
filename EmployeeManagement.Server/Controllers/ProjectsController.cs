using EmployeeManagement.Server.DTOs;
using EmployeeManagement.Server.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EmployeeManagement.Server.Controllers
{
    // Dy rolet mund te lexojne; veprimet e shkrimit me poshte kerkojne Administrator.
    [ApiController]
    [Route("api/projects")]
    [Authorize(Roles = "Administrator,Employee")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public class ProjectsController : ControllerBase
    {
        private readonly IProjectService _projectService;

        // Controller varet nga interface; implementimi konkret regjistrohet ne Program.cs.
        public ProjectsController(IProjectService projectService)
        {
            _projectService = projectService;
        }

        // GET /api/projects kthen 200 me projektet e aksesueshme, edhe kur lista eshte bosh.
        [HttpGet]
        [ProducesResponseType(typeof(List<ProjectDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<ProjectDto>>> GetProjects()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            // Klienti nuk mund te zgjedhe userId ose rolin permes query string-ut.
            return Ok(await _projectService.GetProjectsAsync(userId, User.IsInRole("Administrator")));
        }

        // GET /api/projects/{id} kthen te njejtin 404 per mungesen dhe per projektin e palejuar.
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ProjectDto>> GetProjectById(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            var project = await _projectService.GetProjectByIdAsync(id, userId, User.IsInRole("Administrator"));
            if (project == null)
                return NotFound();

            return Ok(project);
        }

        // POST krijon projektin; annotations e DTO-se kontrollohen automatikisht nga ApiController.
        [HttpPost]
        [Authorize(Roles = "Administrator")]
        [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<ProjectDto>> CreateProject(ProjectRequestDto request)
        {
            var result = await _projectService.CreateProjectAsync(request);
            if (result.Status != ProjectOperationStatus.Success)
                return FailureResponse(result);

            return CreatedAtAction(nameof(GetProjectById), new { id = result.Project!.Id }, result.Project);
        }

        // PUT ndryshon emrin/pershkrimin; anetaresimet dhe data e krijimit ruhen.
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Administrator")]
        [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<ProjectDto>> UpdateProject(int id, ProjectRequestDto request)
        {
            var result = await _projectService.UpdateProjectAsync(id, request);
            if (result.Status != ProjectOperationStatus.Success)
                return FailureResponse(result);
            return Ok(result.Project);
        }

        // DELETE kthen 409 kur projekti ka detyra te paperfunduara; ne sukses kthen 204.
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Administrator")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> DeleteProject(int id)
        {
            var result = await _projectService.DeleteProjectAsync(id);
            return result.Status == ProjectOperationStatus.Success ? NoContent() : FailureResponse(result);
        }

        // Shtimi lejohet vetem per Employee ekzistues; service ndalon anetaresimet e dyfishta.
        [HttpPost("{projectId:int}/employees/{userId}")]
        [Authorize(Roles = "Administrator")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> AddEmployee(int projectId, string userId)
        {
            var result = await _projectService.AddEmployeeAsync(projectId, userId);
            if (result.Status != ProjectOperationStatus.Success)
                return FailureResponse(result);
            return CreatedAtAction(nameof(GetProjectEmployees), new { projectId }, null);
        }

        // Heqja nuk fshin perdoruesin ose detyrat; detyrat e caktuara ne projekt japin konflikt.
        [HttpDelete("{projectId:int}/employees/{userId}")]
        [Authorize(Roles = "Administrator")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> RemoveEmployee(int projectId, string userId)
        {
            var result = await _projectService.RemoveEmployeeAsync(projectId, userId);
            return result.Status == ProjectOperationStatus.Success ? NoContent() : FailureResponse(result);
        }

        // Employee lexon anetaret vetem brenda projektit te vet; Administratori lexon cdo projekt.
        [HttpGet("{projectId:int}/employees")]
        [ProducesResponseType(typeof(List<ProjectEmployeeDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<List<ProjectEmployeeDto>>> GetProjectEmployees(int projectId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            var employees = await _projectService.GetProjectEmployeesAsync(projectId, userId, User.IsInRole("Administrator"));
            if (employees == null)
                return NotFound();
            return Ok(employees);
        }

        // Service raporton arsyen; vetem controller zgjedh pergjigjen HTTP.
        private ActionResult FailureResponse(ProjectOperationResult result)
            => result.Status switch
            {
                ProjectOperationStatus.NotFound => NotFound(new { message = result.Error }),
                ProjectOperationStatus.Conflict => Conflict(new { message = result.Error }),
                _ => BadRequest(new { message = result.Error })
            };
    }
}
