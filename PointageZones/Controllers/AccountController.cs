using System.Data;
using System.Diagnostics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PointageZones.Areas.Identity.Data;
using PointageZones.Models;

namespace PointageZones.Controllers
{

    public class AccountController : Controller
    {
        private readonly SignInManager<User> _signInManager;
        private readonly UserManager<User> _userManager;

        public AccountController(SignInManager<User> signInManager, UserManager<User> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }
        
       
        // Action pour afficher la page de connexion
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // Action pour traiter la soumission du formulaire de connexion
        [HttpPost]
        public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
        {
            //ViewData["ReturnUrl"] = returnUrl;

            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(username, password, isPersistent: false, lockoutOnFailure: false);

                if (result.Succeeded)
                {
                    return await RedirectToLocal(username, returnUrl);
                    /*var user = _signInManager.UserManager.Users.Where(a => a.UserName == username).FirstOrDefault();
                    if (user != null)
                    {
                        // Rediriger vers l'URL de retour ou la page d'accueil
                        if (await _userManager.IsInRoleAsync(user, "admin"))
                        {
                            return RedirectToAction("index","Admin");
                        }
                        return RedirectToAction("index","Agent");
                    }*/
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Nom d'utilisateur ou mot de passe incorrect.");
                    return View();
                }
            }

            // Si le modèle n'est pas valide, réafficher le formulaire avec les erreurs
            return View();
        }

        //// Action pour déconnecter l'utilisateur
        //[HttpPost]
        //public async Task<IActionResult> Logout()
        //{
        //    await _signInManager.SignOutAsync();
        //    return RedirectToAction("Index", "Home");
        //}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            // Sign out the user using the HttpContext and IdentityConstants.ApplicationScheme
            await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);

            // Redirect to the Login page
            return RedirectToAction("Login", "Account");
        }
        // Méthode utilitaire pour rediriger vers l'URL de retour ou la page d'accueil
        private async Task<IActionResult> RedirectToLocal(string username, string? returnUrl)
        {

            if (returnUrl != null && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            var user = await _signInManager.UserManager.Users
                .SingleOrDefaultAsync(a => a.UserName == username);

            if (user != null)
            {
                if (await _userManager.IsInRoleAsync(user, "admin"))
                {
                    return RedirectToAction("index", "Admin");
                }
                return RedirectToAction("index", "Agent");
            }

            // Log that we couldn't find the user
            return RedirectToAction("Login", "Account");
        }

        public IActionResult AccessDenied()
        {
            return View();
        }

    }
}
