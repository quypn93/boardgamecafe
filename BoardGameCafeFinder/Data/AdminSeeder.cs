using Microsoft.AspNetCore.Identity;
using BoardGameCafeFinder.Models.Domain;

namespace BoardGameCafeFinder.Data;

public static class AdminSeeder
{
    public static async Task SeedAdminUserAsync(IServiceProvider serviceProvider)
    {
        var userManager = serviceProvider.GetRequiredService<UserManager<User>>();
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

        // Create Admin role if it doesn't exist
        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole<int>("Admin"));
            logger.LogInformation("Created Admin role");
        }

        // Create User role if it doesn't exist
        if (!await roleManager.RoleExistsAsync("User"))
        {
            await roleManager.CreateAsync(new IdentityRole<int>("User"));
            logger.LogInformation("Created User role");
        }

        // Create default admin user
        const string adminEmail = "admin@bgcfinder.com";
        const string adminPassword = "Admin@123"; // Change this in production!

        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new User
            {
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = "Admin",
                LastName = "User",
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(adminUser, adminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
                logger.LogInformation("Created admin user: {Email}", adminEmail);
            }
            else
            {
                logger.LogError("Failed to create admin user: {Errors}",
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
        else
        {
            // Ensure admin user has Admin role
            if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
                logger.LogInformation("Added Admin role to existing user: {Email}", adminEmail);
            }
        }
    }
}
