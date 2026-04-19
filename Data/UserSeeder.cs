using Microsoft.AspNetCore.Identity;
using MoneyTransfer.Constants;
using MoneyTransfer.Models;

namespace MoneyTransfer.Data
{
    public class UserSeeder
    {
        public static async Task SeedUsersAsync(IServiceProvider serviceProvider)
        {
            var userManager = serviceProvider.GetRequiredService<UserManager<User>>();
            await CreateUserWithRole(userManager,
                "admin@ceederpay.com",
                "Admin@123",
                "Admin",
                "User",
                Roles.Admin);

            await CreateUserWithRole(userManager,
                "user@ceederpay.com",
                "User@123",
                "Regular",
                "User",
                Roles.User);

            await CreateUserWithRole(userManager,
                "agent@ceederpay.com",
                "Agent@123",
                "Agent",
                "User",
                Roles.Agent);
        }

        private static async Task CreateUserWithRole(
            UserManager<User> userManager,
            string email,
            string password,
            string firstName,
            string lastName,
            string role)
        {
            if (await userManager.FindByEmailAsync(email) == null)
            {
                var user = new User
                {
                    Email = email,
                    UserName = email,
                    EmailConfirmed = true,
                    FirstName = firstName,
                    LastName = lastName,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                var result = await userManager.CreateAsync(user, password);

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, role);
                }
            }
        }

    }
}
