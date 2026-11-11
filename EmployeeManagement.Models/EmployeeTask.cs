
using System.ComponentModel.DataAnnotations;

namespace EmployeeManagement.Models
{
    // Perfaqeson nje detyre ne nje projekt. Emri e dallon nga tipi Task i .NET per pune asinkrone.
    // Ruan gjendjen dhe perdoruesit e lidhur; lejet per ndryshimin e saj duhen kontrolluar nga service.
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
        // ProjectId eshte foreign key, pra ID-ja e projektit; Project eshte objekti i lidhur ne C#.
        public int ProjectId { get; set; }

        public Project Project { get; set; } = null!;

        // Shenja ? lejon null: nje task mund te mos kete ende nje punonjes te caktuar.
        public string? AssignedToUserId { get; set; }

        public ApplicationUser? AssignedToUser { get; set; }

        // UTC shmang varesine nga zona kohore e serverit. CompletedAt mbetet null derisa task-u te mbyllet.
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAt { get; set; }
        // Krijuesi dhe punonjesi i caktuar jane dy lidhje te ndryshme me ApplicationUser.
        public string? CreatedByUserId { get; set; }

        public ApplicationUser? CreatedByUser { get; set; }
    }
}
