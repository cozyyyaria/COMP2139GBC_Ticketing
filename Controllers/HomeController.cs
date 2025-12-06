using Microsoft.AspNetCore.Mvc;
using Serilog;
using Microsoft.EntityFrameworkCore;
using GBC_Ticketing.Web.Models;

namespace GBC_Ticketing.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Loads /Views/Home/Index.cshtml
        public async Task<IActionResult> Index()
        {
            var events = await _context.Events
                .Include(e => e.Category)
                .OrderBy(e => e.EventDate)
                .Take(10)
                .ToListAsync();
            return View(events);
        }

        [HttpGet]
        public async Task<IActionResult> SearchEvents(string search)
        {
            var query = _context.Events
                .Include(e => e.Category)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(e => e.Title.Contains(search));
            }

            var events = await query
                .OrderBy(e => e.EventDate)
                .ToListAsync();

            return PartialView("_EventPartial", events);
        }

        // Loads /Views/Home/Events.cshtml
        public IActionResult Events() => View();
        
        
        public IActionResult Privacy() 
        {
            return View();
        }

        public IActionResult Error(int? statusCode = null)
        {
            var status = statusCode ?? 500;
            var viewModel = new ErrorViewModel
            {
                StatusCode = status,
                RequestId = HttpContext.TraceIdentifier
            };

            if (status == 404)
            {
                Log.Warning("404 Not Found: {Path}", HttpContext.Request.Path);
                return View("NotFound", viewModel);
            }
            else if (status == 500)
            {
                Log.Error("500 Internal Server Error: {Path}", HttpContext.Request.Path);
                return View("ServerError", viewModel);
            }

            Log.Warning("Error {StatusCode}: {Path}", status, HttpContext.Request.Path);
            return View(viewModel);
        }
    }
}

