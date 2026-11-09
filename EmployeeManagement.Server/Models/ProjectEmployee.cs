using System.ComponentModel.DataAnnotations;

namespace EmployeeManagement.Server.Models
{
    public class ProjectEmployee
    {
        public int ProjectId { get; set; }

        public Project Project { get; set; } = null!;

        [Required]
        public string UserId { get; set; } = string.Empty;

        public ApplicationUser User { get; set; } = null!;
    }
}
