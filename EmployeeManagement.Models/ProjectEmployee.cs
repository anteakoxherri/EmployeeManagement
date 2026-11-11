using System.ComponentModel.DataAnnotations;

namespace EmployeeManagement.Models
{
    // Tabela lidhese midis perdoruesve dhe projekteve: nje rresht tregon nje anetaresim.
    // Keshtu nje punonjes mund te jete ne shume projekte dhe nje projekt mund te kete shume punonjes.
    public class ProjectEmployee
    {
        // ProjectId dhe UserId formojne bashke celesin kryesor, te konfiguruar ne DbContext.
        // I njejti perdorues nuk mund te shtohet dy here ne te njejtin projekt.
        public int ProjectId { get; set; }

        public Project Project { get; set; } = null!;

        [Required]
        public string UserId { get; set; } = string.Empty;

        // null! shmang paralajmerimin e inicializimit: EF Core e mbush lidhjen kur query e ngarkon.
        public ApplicationUser User { get; set; } = null!;

    }
}
