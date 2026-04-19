using Microsoft.AspNetCore.Identity;
using MoneyTransfer.Constants;

namespace MoneyTransfer.Data
{
    public class RoleSeeder
    {
        public static async Task SeedRolesAsync(IServiceProvider serviceProvider) 
        { 
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            if(!await roleManager.RoleExistsAsync(Roles.Admin))
            {
                await roleManager.CreateAsync(new IdentityRole(Roles.Admin));
            }

            if(!await roleManager.RoleExistsAsync(Roles.User))
            {
                await roleManager.CreateAsync(new IdentityRole(Roles.User));
            }
             if(!await roleManager.RoleExistsAsync(Roles.Agent))
            {
                await roleManager.CreateAsync(new IdentityRole(Roles.Agent));
            }
        }
    }
}
