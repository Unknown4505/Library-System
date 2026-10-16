using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BookKiosk.CMS.Helpers;

namespace BookKiosk.CMS.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            // Admin → Dashboard; Staff → không được xem dashboard doanh thu → POS
            if (!User.IsAdmin())
            {
                return RedirectToAction("Index", "Pos");
            }
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }
    }
}
