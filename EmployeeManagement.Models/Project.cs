
using System.ComponentModel.DataAnnotations;

namespace EmployeeManagement.Models
{
    // Perfaqeson nje projekt ne databaze. DbContext e lidh kete klase me tabelen Projects.
    // Modeli pershkruan te dhenat; rregullat e aksesit dhe veprimet API vendosen te services/controllers.
    public class Project
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        // Annotations siper fushes tregojne se emri eshte i detyrueshem dhe ka maksimumi 100 karaktere.
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        // Nje projekt lidhet me shume anetaresime dhe task-e; lidhjet percaktohen ne OnModelCreating.
        public ICollection<ProjectEmployee> ProjectEmployees { get; set; } = new List<ProjectEmployee>();

        public ICollection<EmployeeTask> Tasks { get; set; } = new List<EmployeeTask>();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
