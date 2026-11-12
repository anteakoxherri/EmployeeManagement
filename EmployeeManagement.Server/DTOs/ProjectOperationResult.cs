namespace EmployeeManagement.Server.DTOs
{
    // Rezultat i brendshem i service-it; controller e perkthen ne kodin HTTP perkates.
    public enum ProjectOperationStatus { Success, NotFound, Invalid, Conflict }

    public class ProjectOperationResult
    {
        public ProjectOperationStatus Status { get; set; }
        public ProjectDto? Project { get; set; }
        public string? Error { get; set; }
    }
}
