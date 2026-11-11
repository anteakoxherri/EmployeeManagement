
using EmployeeManagement.Models;
using Microsoft.AspNetCore.Identity;

namespace EmployeeManagement.Server.Data
{
    // Pergatit rolet dhe administratorin fillestar gjate nisjes; nuk eshte endpoint publik regjistrimi.
    // Kontrollet e ekzistences lejojne nisjen perseri pa krijuar te njejtat te dhena nga e para.
    public static class DbSeeder
    {
        // Merr sherbimet e scope-it dhe konfigurimin; ruan te dhenat qe mungojne dhe nuk kthen vlere.
        // Task lejon Program.cs ta prese me await pa bllokuar nje thread gjate pritjes se databazes.
        public static async Task SeedAsync(
            IServiceProvider services,
            IConfiguration configuration)
        {
            // Marrim mjetet e Identity per menaxhimin e roleve dhe perdoruesve
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

            // Percaktojme dy rolet qe do te kete aplikacioni
            string[] roles = { "Administrator", "Employee" };

            // Kontrollojme nese rolet ekzistojne dhe krijojme ato qe mungojne
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    var result = await roleManager.CreateAsync(new IdentityRole(role));

                    if (!result.Succeeded)
                        throw new Exception(
                            string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }

            // Marrim kredencialet nga konfigurimi (p.sh. User Secrets), pa i vendosur ne kod ose ne log.
            var email = configuration["SeedAdmin:Email"];
            var password = configuration["SeedAdmin:Password"];

            // Kontrollojme nese jane vendosur kredencialet e adminit
            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    "Admin email and password must be configured.");
            }

            // Kontrollojme nese administratori ekziston ne databaze
            var admin = await userManager.FindByEmailAsync(email);

            if (admin == null)
            {
                // Krijojme administratorin fillestar
                admin = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FirstName = "System",
                    LastName = "Administrator",
                    EmailConfirmed = true
                };

                // UserManager kontrollon rregullat dhe ruan hash-in, jo password-in ne tekst te lexueshem.
                var result = await userManager.CreateAsync(admin, password);

                if (!result.Succeeded)
                    throw new Exception(
                        string.Join(", ", result.Errors.Select(e => e.Description)));
            }

            // I caktojme rolin Administrator nese nuk e ka
            if (!await userManager.IsInRoleAsync(admin, "Administrator"))
            {
                var result = await userManager.AddToRoleAsync(admin, "Administrator");

                if (!result.Succeeded)
                    throw new Exception(
                        string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}
