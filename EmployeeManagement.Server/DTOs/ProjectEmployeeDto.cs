namespace EmployeeManagement.Server.DTOs
{
    // Lista e anetareve ekspozon vetem identitetin dhe kontaktin, pa te dhena sensitive te Identity.
    public class ProjectEmployeeDto
    {
        public string Id { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Email { get; set; }
    }
}
