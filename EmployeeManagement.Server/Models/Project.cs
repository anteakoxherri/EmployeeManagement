
using System.ComponentModel.DataAnnotations;

namespace EmployeeManagement.Server.Models
{
    public class Project
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public ICollection<ProjectEmployee> ProjectEmployees { get; set; } = new List<ProjectEmployee>();

        public ICollection<EmployeeTask> Tasks { get; set; } = new List<EmployeeTask>();
    }
}
