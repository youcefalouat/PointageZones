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

        // Ajouter les agents
        var users = new[]
        {
            new { UserName = "Agent1", Password = "Agent123." },
            new { UserName = "Agent2", Password = "Agent123." },
            new { UserName = "Agent3", Password = "Agent123." }
        };

        foreach (var user in users)
        {
            var existingUser = await userManager.FindByNameAsync(user.UserName);
            if (existingUser == null)
            {
                var newUser = new User
                {
                    UserName = user.UserName,
                };

                var userResult = await userManager.CreateAsync(newUser, user.Password);
                if (!userResult.Succeeded)
                {
                    foreach (var error in userResult.Errors)
                    {
                        Console.WriteLine($"Erreur lors de la création de l'utilisateur {user.UserName}: {error.Description}");
                    }
                }
            }
        }
    }
}
