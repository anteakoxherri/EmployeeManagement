using System.ComponentModel.DataAnnotations;

namespace EmployeeManagement.Server.DTOs
{
    // Klienti dergon vetem fushat e lejuara; krijuesi dhe datat caktohen nga serveri.
    public class CreateTaskDto
    {
        [Required]
        [StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Range(1, int.MaxValue)]
        public int ProjectId { get; set; }

        // null lejon detyre ende te pacaktuar, sic e parashikon modeli ekzistues.
        [StringLength(450, MinimumLength = 1)]
        public string? AssignedToUserId { get; set; }
    }
}
