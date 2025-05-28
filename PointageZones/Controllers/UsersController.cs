using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PointageZones.Areas.Identity.Data;
using PointageZones.Data;
using PointageZones.Models;
using static PointageZones.Controllers.UsersController;

namespace PointageZones.Controllers
{
    [Authorize(Roles = "admin")]
    public class UsersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> userManager;
        private readonly RoleManager<IdentityRole> _roleManager;


        public UsersController(ApplicationDbContext context, IServiceProvider serviceProvider) 
        {
            _context = context;
            userManager = serviceProvider.GetRequiredService<UserManager<User>>();
            _roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        }

        public class UserRoleViewModel
        {
            public User User { get; set; }
            public string Role { get; set; }
        }


        // Get: Users
        public async Task<IActionResult> Index()
        {
            var users = await _context.Users.Where(u => u.IsLocked != true).ToListAsync();
            var userRolesViewModel = new List<UserRoleViewModel>();

            foreach (var user in users)
            {
                var roles = await userManager.GetRolesAsync(user);
                var roleName = roles.FirstOrDefault(); // Get the first (and only) role

                userRolesViewModel.Add(new UserRoleViewModel
                {
                    User = user,
                    Role = roleName // Store the single role name
                });
            }

            return View(userRolesViewModel);
        }
    

    

        // GET: users/Details/5
        public async Task<IActionResult> Details(string? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(m => m.Id == id);
            if (user == null)
            {
                return NotFound();
            }
            var roles = await userManager.GetRolesAsync(user);
            var roleName = roles.FirstOrDefault(); // Get the first (and only) role

            var userRolesViewModel = new UserRoleViewModel
            {
                User = user,
                Role = roleName // Store the single role name
            };
            return View(userRolesViewModel);
        }

        // GET: User/Create
        public IActionResult Create()
        {
            // Récupérer tous les rôles disponibles
            var roles = _roleManager.Roles.Select(r => r.Name).ToList();
            ViewBag.Roles = roles;

            return View();
        }

        // POST: User/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Nom,Prenom,UserName,PasswordHash")] User user, string? Role)
        {
            var existingUser = await userManager.FindByNameAsync(user.UserName);
            if (existingUser == null)
            {
                var newUser = new User
                {
                    Nom = user.Nom,
                    Prenom = user.Prenom,
                    UserName = user.UserName,
                };

                var userResult = await userManager.CreateAsync(newUser, user.PasswordHash);
                if (!userResult.Succeeded)
                {
                    foreach (var error in userResult.Errors)
                    {
                        Console.WriteLine($"Erreur lors de la création de l'utilisateur {user.UserName}: {error.Description}");
                        ModelState.AddModelError("", error.Description);
                    }

                    // Récupérer tous les rôles disponibles
                    var roles = _roleManager.Roles.Select(r => r.Name).ToList();
                    ViewBag.Roles = roles;

                    // Create ViewModel for returning to the view
                    var userRolesViewModel = new UserRoleViewModel
                    {
                        User = user,
                        Role = Role
                    };

                    TempData["Notification"] = "Erreur lors de la création de l'utilisateur " + user.UserName;
                    return View(userRolesViewModel);
                }

                // Ajouter le rôle si spécifié
                if (!string.IsNullOrEmpty(Role))
                {
                    await userManager.AddToRoleAsync(newUser, Role);
                }

                TempData["Notification"] = "Utilisateur Créé avec succès";
                return RedirectToAction("Index");
            }

            // Si l'utilisateur existe déjà
            ModelState.AddModelError("", "Ce User existe déjà !");

            // Récupérer tous les rôles disponibles
            var availableRoles = _roleManager.Roles.Select(r => r.Name).ToList();
            ViewBag.Roles = availableRoles;

            // Create ViewModel for returning to the view
            var existingUserViewModel = new UserRoleViewModel
            {
                User = user,
                Role = Role
            };

            return View(existingUserViewModel);
        }

        // GET: users/Edit/5
        public async Task<IActionResult> Edit(string? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            // Récupérer le rôle actuel de l'utilisateur
            var userRoles = await userManager.GetRolesAsync(user);
            var currentRole = userRoles.FirstOrDefault();

            // Récupérer tous les rôles disponibles
            var roles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();

            ViewBag.Roles = roles;
            ViewBag.CurrentRole = currentRole;



            return View(user);
        }

        // POST: users/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Edit(string id, [Bind("Id,Nom,Prenom,UserName,PasswordHash")] User user, string? NewPassword)
        //{
        //    if (id != user.Id)
        //    {
        //        return NotFound();
        //    }

        //    if (ModelState.IsValid)
        //    {
        //         try
        //         {
        //            var existingUser = await userManager.FindByIdAsync(id);
        //            if (existingUser == null)
        //            {
        //                return NotFound();
        //            }

        //            existingUser.Nom = user.Nom;
        //            existingUser.Prenom = user.Prenom;
        //            existingUser.UserName = user.UserName;

        //            IdentityResult? result;

        //            // Check if admin provided a new password
        //            if (!string.IsNullOrWhiteSpace(NewPassword))
        //            {
        //                // Remove old password
        //                await userManager.RemovePasswordAsync(existingUser);

        //                // Set new password
        //                result = await userManager.AddPasswordAsync(existingUser, NewPassword);
        //                if (!result.Succeeded)
        //                {
        //                    foreach (var error in result.Errors)
        //                    {
        //                        ModelState.AddModelError("", error.Description);
        //                    }
        //                    return View(user);
        //                }
        //            }

        //            // Check if admin provided a new password
        //            if (!string.IsNullOrWhiteSpace(NewPassword))
        //            {
        //                // Remove old password
        //                await userManager.RemovePasswordAsync(existingUser);

        //                // Set new password
        //                result = await userManager.AddPasswordAsync(existingUser, NewPassword);
        //                if (!result.Succeeded)
        //                {
        //                    foreach (var error in result.Errors)
        //                    {
        //                        ModelState.AddModelError("", error.Description);
        //                    }
        //                    return View(user);
        //                }
        //            }


        //            var result2 = await userManager.UpdateAsync(existingUser);
        //            if (!result2.Succeeded)
        //            {
        //                foreach (var error in result2.Errors)
        //                {
        //                    ModelState.AddModelError("", error.Description);
        //                }
        //                return View(user);
        //            }
        //            TempData["Notification"] = "Utilisateur Modifié avec succés";
        //            return RedirectToAction("Index");
        //        }
        //         catch (DbUpdateConcurrencyException)
        //         {
        //             if (!userExists(user.Id))
        //             {
        //                 return NotFound();
        //             }
        //             else
        //             {
        //                 throw;
        //             }
        //         }

        //    }
        //    return RedirectToAction("Index");
        //}

        // POST: users/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("Id,Nom,Prenom,UserName,PasswordHash")] User user, string? NewPassword, string? Role)
        {
            if (id != user.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingUser = await userManager.FindByIdAsync(id);
                    if (existingUser == null)
                    {
                        return NotFound();
                    }

                    existingUser.Nom = user.Nom;
                    existingUser.Prenom = user.Prenom;
                    existingUser.UserName = user.UserName;

                    // Gérer le changement de rôle
                    if (!string.IsNullOrEmpty(Role))
                    {
                        var userRoles = await userManager.GetRolesAsync(existingUser);
                        await userManager.RemoveFromRolesAsync(existingUser, userRoles);
                        await userManager.AddToRoleAsync(existingUser, Role);
                    }

                    // Gérer le changement de mot de passe
                    if (!string.IsNullOrWhiteSpace(NewPassword))
                    {
                        await userManager.RemovePasswordAsync(existingUser);
                        var passwordResult = await userManager.AddPasswordAsync(existingUser, NewPassword);
                        if (!passwordResult.Succeeded)
                        {
                            foreach (var error in passwordResult.Errors)
                            {
                                ModelState.AddModelError("", error.Description);
                            }

                            // Prepare ViewModel for returning to the view
                            var userRolesViewModel = new UserRoleViewModel
                            {
                                User = user,
                                Role = Role
                            };

                            // Récupérer tous les rôles disponibles
                            var roles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
                            ViewBag.Roles = roles;
                            ViewBag.CurrentRole = Role;

                            return View(userRolesViewModel);
                        }
                    }

                    var result = await userManager.UpdateAsync(existingUser);
                    if (!result.Succeeded)
                    {
                        foreach (var error in result.Errors)
                        {
                            ModelState.AddModelError("", error.Description);
                        }

                        // Prepare ViewModel for returning to the view
                        var userRolesViewModel = new UserRoleViewModel
                        {
                            User = user,
                            Role = Role
                        };

                        // Récupérer tous les rôles disponibles
                        var roles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
                        ViewBag.Roles = roles;
                        ViewBag.CurrentRole = Role;

                        return View(userRolesViewModel);
                    }

                    TempData["Notification"] = "Utilisateur modifié avec succès";
                    return RedirectToAction("Index");
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!userExists(user.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            // If we got this far, something failed, redisplay form
            // Prepare ViewModel for returning to the view
            var errorViewModel = new UserRoleViewModel
            {
                User = user,
                Role = Role
            };

            // Récupérer tous les rôles disponibles
            var availableRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
            ViewBag.Roles = availableRoles;
            ViewBag.CurrentRole = Role;

            return View(errorViewModel);
        }

        // GET: Users/Delete/5
        public async Task<IActionResult> Delete(string? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(m => m.Id == id);
            if (user == null)
            {
                return NotFound();
            }

            // Récupérer le rôle de l'utilisateur
            var roles = await userManager.GetRolesAsync(user);
            var roleName = roles.FirstOrDefault(); // Get the first (and only) role

            var userRolesViewModel = new UserRoleViewModel
            {
                User = user,
                Role = roleName // Store the single role name
            };

            return View(userRolesViewModel);
        }

        // POST: Users/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                TempData["Error"] = "Utilisateur introuvable.";
                return RedirectToAction("Index");
            }

            user.IsLocked = true;
            var result = await userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }

                // Récupérer le rôle de l'utilisateur
                var roles = await userManager.GetRolesAsync(user);
                var roleName = roles.FirstOrDefault(); // Get the first (and only) role

                var userRolesViewModel = new UserRoleViewModel
                {
                    User = user,
                    Role = roleName // Store the single role name
                };

                return View(userRolesViewModel);
            }

            TempData["Notification"] = "Utilisateur désactivé avec succès.";
            return RedirectToAction("Index");
        }


        private bool userExists(string id)
        {
            return _context.Users.Any(e => e.Id == id);
        }

    }
}
