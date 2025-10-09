using Microsoft.AspNetCore.Mvc;

namespace GBC_Ticketing.Web.Controllers
{
    public class HomeController : Controller
    {
        // Loads /Views/Home/Index.cshtml
        public IActionResult Index() => View();

        // Loads /Views/Home/Events.cshtml
        public IActionResult Events() => View();
        
        
        public IActionResult Privacy() 
        {
            return View();
        }
        
        
        
    }
}

