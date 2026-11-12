namespace EmployeeManagement.Server.DTOs
{
    // Pergjigjja nuk ekspozon entitetet/navigimet ose te dhena te brendshme te Identity.
    public class TaskDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int ProjectId { get; set; }
        public string? AssignedToUserId { get; set; }
        public string? CreatedByUserId { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
