using EmployeeManagement.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagement.Server.Data
{
    // Ura midis modeleve C# dhe SQL Server. Services e perdorin per query dhe ruajtje me EF Core.
    // IdentityDbContext perfshin edhe tabelat e perdoruesve, roleve dhe lidhjeve te Identity.
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        // DI dergon opsionet nga Program.cs, perfshire provider-in SQL Server dhe connection string.
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // DbSet eshte pika nga ku nisim query LINQ per nje lloj entiteti; query perkthehet ne SQL.
        public DbSet<Project> Projects { get; set; }

        public DbSet<ProjectEmployee> ProjectEmployees { get; set; }

        public DbSet<EmployeeTask> EmployeeTasks { get; set; }

        // EF Core e therrret per te ndertuar modelin e databazes, jo per te ndryshuar rreshta.
        // builder merr rregullat e lidhjeve; metodat Up/Down te migrations zbatojne ndryshimet ne SQL.
        protected override void OnModelCreating(ModelBuilder builder)
        {
            // Ruajme fillimisht konfigurimin e Identity para se te shtojme lidhjet tona.
            base.OnModelCreating(builder);

            // Celesi i perbere identifikon nje anetaresim me kombinimin projekt/perdorues.
            builder.Entity<ProjectEmployee>()
                .HasKey(membership => new { membership.ProjectId, membership.UserId });

            builder.Entity<ProjectEmployee>()
                // Cascade heq rreshtat e anetaresimit kur hiqet objekti prind.
                .HasOne(membership => membership.Project)
                .WithMany(project => project.ProjectEmployees)
                .HasForeignKey(membership => membership.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ProjectEmployee>()
                .HasOne(membership => membership.User)
                .WithMany(user => user.ProjectEmployees)
                .HasForeignKey(membership => membership.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<EmployeeTask>()
                // Restrict bllokon fshirjen e projektit sa kohe ekzistojne task-e te lidhura.
                // Kontrolli specifik per task-e te hapura do te kerkoje edhe logjike ne service.
                .HasOne(task => task.Project)
                .WithMany(project => project.Tasks)
                .HasForeignKey(task => task.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<EmployeeTask>()
                // SetNull ruan task-un, por heq caktimin kur fshihet perdoruesi i caktuar.
                .HasOne(task => task.AssignedToUser)
                .WithMany(user => user.AssignedTasks)
                .HasForeignKey(task => task.AssignedToUserId)
                .OnDelete(DeleteBehavior.SetNull);
           builder.Entity<EmployeeTask>()
                // Restrict ruan lidhjen e historikut: krijuesi nuk fshihet pa trajtuar me pare keto task-e.
                .HasOne(task => task.CreatedByUser)
                .WithMany()
                .HasForeignKey(task => task.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
