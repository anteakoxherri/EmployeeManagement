namespace EmployeeManagement.Server.DTOs
{
    // Formati i sigurt qe API kthen per nje perdorues. DTO ndan kontraten HTTP nga entiteti i databazes.
    // Nuk perfshin PasswordHash, SecurityStamp ose te dhena te tjera te brendshme te Identity.
    public class UserDto
    {
        public string Id { get; set; } = string.Empty;

        public string? UserName { get; set; }

        // ? pas string lejon null dhe pasqyron tipin e fushes ne Identity.
        public string? Email { get; set; }

        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public string? ProfilePicturePath { get; set; }

        // Rolet lexohen nga tabelat lidhese te Identity, sepse perdoruesi mund te kete me shume se nje rol.
        public List<string> Roles { get; set; } = new();
    }
}
