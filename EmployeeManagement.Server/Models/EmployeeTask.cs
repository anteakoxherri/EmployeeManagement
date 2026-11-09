
using System.ComponentModel.DataAnnotations;

namespace EmployeeManagement.Server.Models
{
    public class EmployeeTask
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public bool IsCompleted { get; set; } = false;

        [Required]
        public int ProjectId { get; set; }

        public Project Project { get; set; } = null!;

        public string? AssignedToUserId { get; set; }

        public ApplicationUser? AssignedToUser { get; set; }
    }
}
