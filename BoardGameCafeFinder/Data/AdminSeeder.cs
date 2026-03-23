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

        // Create CafeOwner role if it doesn't exist
        if (!await roleManager.RoleExistsAsync("CafeOwner"))
        {
            await roleManager.CreateAsync(new IdentityRole<int>("CafeOwner"));
            logger.LogInformation("Created CafeOwner role");
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

        // Create test CafeOwner user
        const string ownerEmail = "owner@bgcfinder.com";
        const string ownerPassword = "Owner@123";

        var ownerUser = await userManager.FindByEmailAsync(ownerEmail);
        if (ownerUser == null)
        {
            ownerUser = new User
            {
                UserName = ownerEmail,
                Email = ownerEmail,
                FirstName = "Cafe",
                LastName = "Owner",
                DisplayName = "Test Cafe Owner",
                IsCafeOwner = true,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(ownerUser, ownerPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(ownerUser, "CafeOwner");
                logger.LogInformation("Created cafe owner user: {Email}", ownerEmail);
            }
            else
            {
                logger.LogError("Failed to create cafe owner user: {Errors}",
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
        else
        {
            if (!await userManager.IsInRoleAsync(ownerUser, "CafeOwner"))
            {
                await userManager.AddToRoleAsync(ownerUser, "CafeOwner");
            }
        }

        // Create test regular User
        const string userEmail = "user@bgcfinder.com";
        const string userPassword = "User@123";

        var regularUser = await userManager.FindByEmailAsync(userEmail);
        if (regularUser == null)
        {
            regularUser = new User
            {
                UserName = userEmail,
                Email = userEmail,
                FirstName = "Test",
                LastName = "User",
                DisplayName = "Test User",
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(regularUser, userPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(regularUser, "User");
                logger.LogInformation("Created regular user: {Email}", userEmail);
            }
            else
            {
                logger.LogError("Failed to create regular user: {Errors}",
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
        else
        {
            if (!await userManager.IsInRoleAsync(regularUser, "User"))
            {
                await userManager.AddToRoleAsync(regularUser, "User");
            }
        }
    }
}
