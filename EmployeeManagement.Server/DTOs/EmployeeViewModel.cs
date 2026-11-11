namespace EmployeeManagement.Server.DTOs
{
    // Pergjigjja e /api/employees: jep vetem te dhenat baze qe duhen per listimin e punonjesve.
    // ViewModel ketu ka te njejtin qellim si DTO: shmang dergimin e gjithe entitetit te Identity.
    public class EmployeeViewModel
    {
        // ID-ja perdoret per identifikim; password-i dhe hash-i nuk jane pjese e kesaj kontrate.
        public string Id { get; set; } = string.Empty;

        public string? UserName { get; set; }

        public string? Email { get; set; }
    }
}
