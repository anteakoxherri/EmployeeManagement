using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace EmployeeManagement.Models
{
    // Perdoruesi zgjeron IdentityUser: Identity menaxhon ID-ne, email-in dhe hash-in e password-it.
    // Ketu shtojme profilin dhe lidhjet; rolet ruhen ne tabelat e Identity, jo si fushe e ketij modeli.
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [StringLength(100)]
        // Required ndalon vleren bosh; StringLength vendos kufirin qe perdor edhe EF Core per kolonen SQL.
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [StringLength(500)]
        public string? ProfilePicturePath { get; set; }

        // Keto navigation properties perfaqesojne lidhjet me projektet dhe detyrat ne modelin EF.
        // Mbushja e tyre gjate leximit varet nga query; nuk ngarkohen domosdoshmerisht automatikisht.
        public ICollection<ProjectEmployee> ProjectEmployees { get; set; } = new List<ProjectEmployee>();

        public ICollection<EmployeeTask> AssignedTasks { get; set; } = new List<EmployeeTask>();
    }
}
