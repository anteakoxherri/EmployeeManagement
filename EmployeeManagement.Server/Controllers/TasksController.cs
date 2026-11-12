using System.Security.Claims;
using EmployeeManagement.Server.DTOs;
using EmployeeManagement.Server.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeManagement.Server.Controllers
{
    // Rolet kontrollohen nga JWT; service kontrollon projektin dhe caktimin per cdo kerkese.
    [ApiController]
    [Route("api/tasks")]
    [Authorize(Roles = "Administrator,Employee")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public class TasksController : ControllerBase
    {
        private readonly ITaskService _taskService;

        public TasksController(ITaskService taskService)
        {
            _taskService = taskService;
        }

        // GET kthen edhe detyrat e kolegeve ne projekt, por nuk u jep leje ndryshimi.
        [HttpGet]
        [ProducesResponseType(typeof(List<TaskDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<TaskDto>>> GetTasks()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
            return Ok(await _taskService.GetTasksAsync(userId, User.IsInRole("Administrator")));
        }

        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<TaskDto>> GetTaskById(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
            var task = await _taskService.GetTaskByIdAsync(id, userId, User.IsInRole("Administrator"));
            if (task == null) return NotFound();
            return Ok(task);
        }

        // Krijuesi merret nga token-i, jo nga body. ApiController validon Data Annotations.
        [HttpPost]
        [ProducesResponseType(typeof(TaskDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<TaskDto>> CreateTask(CreateTaskDto request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
            var result = await _taskService.CreateTaskAsync(request, userId, User.IsInRole("Administrator"));
            if (result.Status != TaskOperationStatus.Success) return FailureResponse(result);
            return CreatedAtAction(nameof(GetTaskById), new { id = result.Task!.Id }, result.Task);
        }

        // Employee mund te ndryshoje vetem detyren qe aktualisht eshte caktuar atij.
        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<TaskDto>> UpdateTask(int id, UpdateTaskDto request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
            var result = await _taskService.UpdateTaskAsync(id, request, userId, User.IsInRole("Administrator"));
            if (result.Status != TaskOperationStatus.Success) return FailureResponse(result);
            return Ok(result.Task);
        }

        // PATCH perfundon detyren pa kerkuar ridergimin e titullit, projektit dhe caktimit.
        [HttpPatch("{id:int}/complete")]
        [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<TaskDto>> CompleteTask(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
            var result = await _taskService.CompleteTaskAsync(id, userId, User.IsInRole("Administrator"));
            if (result.Status != TaskOperationStatus.Success) return FailureResponse(result);
            return Ok(result.Task);
        }

        // Employee nuk lejohet te fshije, edhe kur detyra eshte krijuar prej tij ose caktuar atij.
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Administrator")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> DeleteTask(int id)
        {
            var result = await _taskService.DeleteTaskAsync(id);
            return result.Status == TaskOperationStatus.Success ? NoContent() : FailureResponse(result);
        }

        private ActionResult FailureResponse(TaskOperationResult result)
            => result.Status switch
            {
                TaskOperationStatus.NotFound => NotFound(new { message = result.Error }),
                TaskOperationStatus.Forbidden => Forbid(),
                TaskOperationStatus.Conflict => Conflict(new { message = result.Error }),
                _ => BadRequest(new { message = result.Error })
            };
    }
}
