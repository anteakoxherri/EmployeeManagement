namespace EmployeeManagement.Server.DTOs
{
    // Rezultat i brendshem; service raporton gjendjen, controller percakton HTTP status code.
    public enum TaskOperationStatus { Success, NotFound, Forbidden, Invalid, Conflict }

    public class TaskOperationResult
    {
        public TaskOperationStatus Status { get; set; }
        public TaskDto? Task { get; set; }
        public string? Error { get; set; }
    }
}
