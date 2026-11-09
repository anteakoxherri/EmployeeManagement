using EmployeeManagement.Server.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagement.Server.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Project> Projects { get; set; }

        public DbSet<ProjectEmployee> ProjectEmployees { get; set; }

        public DbSet<EmployeeTask> EmployeeTasks { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ProjectEmployee>()
                .HasKey(membership => new { membership.ProjectId, membership.UserId });

            builder.Entity<ProjectEmployee>()
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
                .HasOne(task => task.Project)
                .WithMany(project => project.Tasks)
                .HasForeignKey(task => task.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<EmployeeTask>()
                .HasOne(task => task.AssignedToUser)
                .WithMany(user => user.AssignedTasks)
                .HasForeignKey(task => task.AssignedToUserId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
