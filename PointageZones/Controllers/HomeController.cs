using Microsoft.AspNetCore.Mvc;
using PointageZones.Areas.Identity.Data;

namespace PointageZones.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            if (User.IsInRole("admin"))
            {
                return RedirectToAction("Index", "Admin");
            }
            else if (User.IsInRole("user"))
            {
                return RedirectToAction("Index", "Agent");
            }

            // Default fallback
            return RedirectToAction("Login", "Account");
        }
    }
}
