using System.ComponentModel.DataAnnotations;

namespace EmployeeManagement.Server.DTOs
{
    // POST dhe PUT ndryshojne vetem emrin/pershkrimin; ID dhe data caktohen nga serveri.
    public class ProjectRequestDto
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }
    }
}
