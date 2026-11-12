namespace EmployeeManagement.Server.DTOs
{
    // Pergjigjja e leximit te projektit; nuk ekspozon perdoruesit ose navigimet e EF Core.
    public class ProjectDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
