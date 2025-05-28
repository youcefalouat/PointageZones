using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using PointageZones.Areas.Identity.Data;
using PointageZones.Models;

public static class SeedData
{
    public static async Task Initialize(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<User>>();

        // Vérifier et créer le rôle "admin" s'il n'existe pas
        string adminRole = "admin";
        if (!await roleManager.RoleExistsAsync(adminRole))
        {
            await roleManager.CreateAsync(new IdentityRole(adminRole));
        }

        string AdminUsername = "Admin";
        string AdminPassword = "AdminSomepharm123.";

        var existingAdmin = await userManager.FindByNameAsync(AdminUsername);
        if (existingAdmin == null)
        {
            var newAdmin = new User
            {
                UserName = AdminUsername,
            };

            var result = await userManager.CreateAsync(newAdmin, AdminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(newAdmin, adminRole);
            }
            else
            {
                foreach (var error in result.Errors)
                {
                    Console.WriteLine($"Erreur lors de la création de l'utilisateur {AdminUsername}: {error.Description}");
                }
            }
        }

        
    }
}
