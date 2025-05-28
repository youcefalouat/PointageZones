using Microsoft.AspNetCore.Mvc;
using PointageZones.Areas.Identity.Data;

namespace PointageZones.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            try
            {
                if (User?.Identity?.IsAuthenticated == true)
                {
                    if (User.IsInRole("admin"))
                    {
                        return RedirectToAction("Index", "Admin");
                    }
                        
                    return RedirectToAction("Index", "Agent");
                }

                return RedirectToAction("Login", "Account");
            }
            catch (Exception ex)
            {
                // Log or temporarily show error
                return Content("Erreur dans HomeController.Index: " + ex.Message);
            }
        }
    }
}
