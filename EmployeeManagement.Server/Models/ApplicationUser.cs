using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace EmployeeManagement.Server.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [StringLength(500)]
        public string? ProfilePicturePath { get; set; }

        public ICollection<ProjectEmployee> ProjectEmployees { get; set; } = new List<ProjectEmployee>();

        public ICollection<EmployeeTask> AssignedTasks { get; set; } = new List<EmployeeTask>();
    }
}
