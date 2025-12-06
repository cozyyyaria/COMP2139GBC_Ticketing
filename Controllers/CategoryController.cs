using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GBC_Ticketing.Web.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace GBC_Ticketing.Web.Controllers
{
    [Authorize(Roles = "Admin,Organizer")]
    public class CategoryController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CategoryController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Category
        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            return View(await _context.Categories.ToListAsync());
        }

        // GET: Category/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();

           
            ViewBag.EventCount = await _context.Events
                .CountAsync(e => e.CategoryId == id);

            return View(category);
        }

        
        
        [HttpGet]
        public IActionResult Create() => View();

        // POST: Category/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id", "Name","Description")] Category category)
        {
            if (!ModelState.IsValid)
            {
                // Log all errors
                foreach (var key in ModelState.Keys)
                {
                    var errors = ModelState[key].Errors;
                    foreach (var error in errors)
                    {
                        Console.WriteLine($"Key: {key}, Error: {error.ErrorMessage}");
                    }
                }
            }

            if (ModelState.IsValid)
            {
                _context.Categories.Add(category);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(category);
        }

        // POST: Category/CreateAjax - For AJAX requests from Event Create page
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> CreateAjax([FromBody] Category category)
        {
            try
            {
                // Check authorization
                if (!User.Identity?.IsAuthenticated ?? true)
                {
                    Response.StatusCode = 401;
                    return Json(new { success = false, errors = new[] { "You must be logged in to create categories." } });
                }
                
                var isAdmin = User.IsInRole("Admin");
                var isOrganizer = User.IsInRole("Organizer");
                
                if (!isAdmin && !isOrganizer)
                {
                    Response.StatusCode = 403;
                    return Json(new { success = false, errors = new[] { "You do not have permission to create categories. Only Administrators and Organizers can create categories." } });
                }
                
                // Validate input
                if (category == null || string.IsNullOrWhiteSpace(category.Name))
                {
                    return Json(new { success = false, errors = new[] { "Category name is required." } });
                }
                
                var trimmedName = category.Name.Trim();
                
                if (trimmedName.Length > 50)
                {
                    return Json(new { success = false, errors = new[] { "Category name must be 50 characters or less." } });
                }
                
                // Check if category with same name already exists
                var existingCategory = await _context.Categories
                    .FirstOrDefaultAsync(c => c.Name.ToLower() == trimmedName.ToLower());
                
                if (existingCategory != null)
                {
                    return Json(new { success = false, errors = new[] { $"A category named '{trimmedName}' already exists." } });
                }
                
                // Create new category
                var newCategory = new Category
                {
                    Name = trimmedName,
                    Description = category.Description?.Trim() ?? string.Empty
                };
                
                _context.Categories.Add(newCategory);
                await _context.SaveChangesAsync();
                
                return Json(new { success = true, category = new { id = newCategory.Id, name = newCategory.Name } });
            }
            catch (Exception ex)
            {
                Response.StatusCode = 500;
                return Json(new { success = false, errors = new[] { $"An error occurred: {ex.Message}" } });
            }
        }

        // GET: Category/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();
            return View(category);
        }

        // POST: Category/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description")] Category category)
        {
            if (id != category.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                _context.Update(category);
                await _context.SaveChangesAsync();


                return RedirectToAction(nameof(Index));
            }

            return View(category);
        }
        
        
        
        
        

        // GET: Category/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();
            return View(category);
        }

        // POST: Category/DeleteConfirmed/5
        [HttpPost, ActionName("DeleteConfirmed")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category != null)
            {
                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}