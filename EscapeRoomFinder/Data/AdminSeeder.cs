using EscapeRoomFinder.Models.Domain;
using Microsoft.AspNetCore.Identity;

namespace EscapeRoomFinder.Data
{
    public class AdminSeeder
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole<int>> _roleManager;

        public AdminSeeder(UserManager<User> userManager, RoleManager<IdentityRole<int>> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task SeedAsync()
        {
            // Create roles
            string[] roles = { "Admin", "User", "VenueOwner" };

            foreach (var role in roles)
            {
                if (!await _roleManager.RoleExistsAsync(role))
                {
                    await _roleManager.CreateAsync(new IdentityRole<int>(role));
                }
            }

            // Create admin user
            var adminEmail = "admin@escaperoomfinder.com";
            var adminUser = await _userManager.FindByEmailAsync(adminEmail);

            if (adminUser == null)
            {
                adminUser = new User
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    DisplayName = "Admin",
                    FirstName = "System",
                    LastName = "Administrator",
                    CreatedAt = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(adminUser, "Admin@123456");

                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }
        }
    }
}
