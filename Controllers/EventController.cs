using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using GBC_Ticketing.Web.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GBC_Ticketing.Web.Controllers;

public class EventController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public EventController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }


    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        int? categoryId,
        DateTime? startDate,
        DateTime? endDate,
        string? availability,
        string? sort = "date",
        string? dir = "asc")
    {
        Console.WriteLine("Date Start: " + startDate);
        Console.WriteLine("Date End: " + endDate);

        var query = _context.Events
            .Include(e => e.Category)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(e => e.Title.Contains(search));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(e => e.CategoryId == categoryId.Value);
        }

        if (startDate.HasValue)
        {
            var startDateUtc = DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc);
            query = query.Where(e => e.EventDate >= startDateUtc);
        }

        if (endDate.HasValue)
        {
            var endDateUtc = DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc);
            query = query.Where(e => e.EventDate <= endDateUtc);
        }

        if (!string.IsNullOrWhiteSpace(availability))
        {
            if (availability.Equals("low", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(e => e.AvailableTickets > 0 && e.AvailableTickets < 5);
            }
            else if (availability.Equals("soldout", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(e => e.AvailableTickets == 0);
            }
            else if (availability.Equals("available", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(e => e.AvailableTickets > 0);
            }
        }

        bool ascending = string.Equals(dir, "asc", StringComparison.OrdinalIgnoreCase);
        query = sort switch
        {
            "title" => ascending ? query.OrderBy(e => e.Title) : query.OrderByDescending(e => e.Title),
            "price" => ascending ? query.OrderBy(e => e.TicketPrice) : query.OrderByDescending(e => e.TicketPrice),
            _ => ascending ? query.OrderBy(e => e.EventDate) : query.OrderByDescending(e => e.EventDate)
        };

        var events = await query.ToListAsync();

        // Populate carousel with upcoming events from this month
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1).AddHours(23).AddMinutes(59).AddSeconds(59);
        
        var upcomingEvents = await _context.Events
            .Include(e => e.Category)
            .Include(e => e.Organizer)
            .Where(e => e.EventDate >= now && e.EventDate >= startOfMonth && e.EventDate <= endOfMonth)
            .OrderBy(e => e.EventDate)
            .Take(5)
            .ToListAsync();
        
        ViewBag.UpcomingEvents = upcomingEvents;
        ViewBag.TotalEvents = events.Count;
        ViewBag.TotalCategories = await _context.Categories.CountAsync();
        ViewBag.LowTicketEvents = events.Count(e => e.AvailableTickets < 5);
        ViewBag.Categories = new SelectList(await _context.Categories.ToListAsync(), "Id", "Name");

        return View(events);
    }


    [Authorize(Roles = "Admin,Organizer")]
    [HttpGet]
    public IActionResult Create()
    {
        ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name");
        return View();
    }


    [Authorize(Roles = "Admin,Organizer")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Event @event, IFormFile? eventImage)
    {
        // Debug: Log the received model
        Console.WriteLine(
            $"Received Event: Title={@event.Title}, CategoryId={@event.CategoryId}, EventDate={@event.EventDate}, TicketPrice={@event.TicketPrice}, AvailableTickets={@event.AvailableTickets}");

        if (@event.CategoryId == 0)
        {
            ModelState.AddModelError("CategoryId", "Category is required.");
        }
        else if (!await _context.Categories.AnyAsync(c => c.Id == @event.CategoryId))
        {
            ModelState.AddModelError("CategoryId", "Selected category does not exist.");
        }

        // Handle DateTime conversion
        if (@event.EventDate != default)
        {
            if (@event.EventDate.Kind == DateTimeKind.Unspecified)
            {
                @event.EventDate = DateTime.SpecifyKind(@event.EventDate, DateTimeKind.Utc);
            }
            else if (@event.EventDate.Kind == DateTimeKind.Local)
            {
                @event.EventDate = @event.EventDate.ToUniversalTime();
            }
        }

        // Auto-assign organizer if user is organizer (not admin)
        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
                if (!isAdmin)
                {
                    @event.OrganizerId = user.Id;
                }
            }
        }

        // Handle image upload
        if (eventImage != null && eventImage.Length > 0)
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extension = Path.GetExtension(eventImage.FileName).ToLowerInvariant();
            
            if (allowedExtensions.Contains(extension))
            {
                var fileName = $"{Guid.NewGuid()}{extension}";
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "events");
                Directory.CreateDirectory(uploadsFolder);
                var filePath = Path.Combine(uploadsFolder, fileName);
                
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await eventImage.CopyToAsync(stream);
                }
                
                @event.ImagePath = $"/images/events/{fileName}";
            }
            else
            {
                ModelState.AddModelError("eventImage", "Please upload a valid image file (.jpg, .png, .gif, .webp)");
            }
        }

        // Debug: Log model state
        Console.WriteLine($"ModelState.IsValid: {ModelState.IsValid}");
        foreach (var key in ModelState.Keys)
        {
            var errors = ModelState[key].Errors;
            foreach (var error in errors)
            {
                Console.WriteLine($"ModelState Error - Key: {key}, Error: {error.ErrorMessage}");
            }
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Events.Add(@event);
                await _context.SaveChangesAsync();
                Console.WriteLine("Event saved successfully!");
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception during save: {ex.Message}");
                ModelState.AddModelError("", "An error occurred while saving the event: " + ex.Message);
            }
        }

        ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name");
        return View(@event);
    }


    [Authorize(Roles = "Admin,Organizer")]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var @event = await _context.Events.FindAsync(id);
        if (@event == null)
        {
            return NotFound();
        }

        // Check if user can edit this event
        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
                if (!isAdmin && @event.OrganizerId != user.Id)
                {
                    return RedirectToAction("AccessDenied", "Account");
                }
            }
        }

        ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", @event.CategoryId);
        return View(@event);
    }


    [Authorize(Roles = "Admin,Organizer")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Event e)
    {
        var existingEvent = await _context.Events.FindAsync(e.Id);
        if (existingEvent == null)
        {
            return NotFound();
        }

        // Check if user can edit this event
        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
                if (!isAdmin && existingEvent.OrganizerId != user.Id)
                {
                    return RedirectToAction("AccessDenied", "Account");
                }
            }
        }

        if (e.EventDate != default && e.EventDate.Kind == DateTimeKind.Unspecified)
        {
            e.EventDate = DateTime.SpecifyKind(e.EventDate, DateTimeKind.Utc);
        }

        if (ModelState.IsValid)
        {
            existingEvent.Title = e.Title;
            existingEvent.CategoryId = e.CategoryId;
            existingEvent.EventDate = e.EventDate;
            existingEvent.TicketPrice = e.TicketPrice;
            existingEvent.AvailableTickets = e.AvailableTickets;
            
            _context.Update(existingEvent);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", e.CategoryId);
        return View(e);
    }

    // GET: Event/Delete/5
    [Authorize(Roles = "Admin,Organizer")]
    public async Task<IActionResult> Delete(int id)
    {
        var ev = await _context.Events
            .Include(e => e.Category)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (ev == null)
        {
            return NotFound();
        }

        // Check if user can delete this event
        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
                if (!isAdmin && ev.OrganizerId != user.Id)
                {
                    return Forbid();
                }
            }
        }

        // Check if there are any tickets associated with this event
        var hasPurchases = await _context.Tickets.AnyAsync(t => t.EventId == id);
        ViewBag.HasPurchases = hasPurchases;

        return View(ev);
    }

    [Authorize(Roles = "Admin,Organizer")]
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        try
        {
            var ev = await _context.Events
                .Include(e => e.Tickets)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (ev == null)
            {
                return NotFound();
            }

            // Check if user can delete this event
            if (User.Identity?.IsAuthenticated == true)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
                    if (!isAdmin && ev.OrganizerId != user.Id)
                    {
                        return Forbid();
                    }
                }
            }

            // Check if there are any tickets associated with this event
            var hasTickets = await _context.Tickets.AnyAsync(t => t.EventId == id);

            if (hasTickets)
            {
                // Get all purchases that have tickets for this event
                var purchasesWithTickets = await _context.Purchases
                    .Where(p => p.Tickets.Any(t => t.EventId == id))
                    .ToListAsync();

                // Delete all tickets for this event first
                var ticketsToDelete = await _context.Tickets
                    .Where(t => t.EventId == id)
                    .ToListAsync();
                _context.Tickets.RemoveRange(ticketsToDelete);

                // Delete all purchases that only had tickets for this event
                foreach (var purchase in purchasesWithTickets)
                {
                    var remainingTickets = await _context.Tickets
                        .AnyAsync(t => t.PurchaseId == purchase.Id);

                    if (!remainingTickets)
                    {
                        _context.Purchases.Remove(purchase);
                    }
                }
            }

            Console.WriteLine($"Deleting event {id}: {ev.Title}");
            _context.Events.Remove(ev);
            await _context.SaveChangesAsync();
            Console.WriteLine("Event and associated data deleted successfully!");

            TempData["Message"] = $"Event '{ev.Title}' and all associated purchases have been deleted.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting event: {ex.Message}");
            TempData["Error"] = $"Error deleting event: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    // GET: Event/Details/5
    [AllowAnonymous] // Explicitly allow anonymous access
    public async Task<IActionResult> Details(int id)
    {
        try
        {
            var ev = await _context.Events
                .Include(e => e.Category)
                .Include(e => e.Organizer)
                .Include(e => e.Tickets)
                .FirstOrDefaultAsync(e => e.Id == id);
            
            if (ev == null)
            {
                Console.WriteLine($"Event with ID {id} not found");
                return NotFound();
            }
            
            Console.WriteLine($"Event Details loaded: {ev.Title}, Organizer: {ev.OrganizerId}");
            return View(ev);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading event details: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return StatusCode(500, "An error occurred while loading event details.");
        }
    }

    [HttpGet]
    public async Task<IActionResult> ListPartial(
        string? search,
        int? categoryId,
        DateTime? startDate,
        DateTime? endDate,
        string? availability,
        string? sort = "date",
        string? dir = "asc")
    {
        var query = _context.Events
            .Include(e => e.Category)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(e => e.Title.Contains(search));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(e => e.CategoryId == categoryId.Value);
        }

        if (startDate.HasValue)
        {
            var normalizedStart = startDate.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc)
                : startDate.Value.ToUniversalTime();
            query = query.Where(e => e.EventDate >= normalizedStart);
        }

        if (endDate.HasValue)
        {
            var normalizedEnd = endDate.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc)
                : endDate.Value.ToUniversalTime();
            query = query.Where(e => e.EventDate <= normalizedEnd);
        }

        if (!string.IsNullOrWhiteSpace(availability))
        {
            if (availability.Equals("low", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(e => e.AvailableTickets > 0 && e.AvailableTickets < 5);
            }
            else if (availability.Equals("soldout", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(e => e.AvailableTickets == 0);
            }
            else if (availability.Equals("available", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(e => e.AvailableTickets > 0);
            }
        }

        bool ascending = string.Equals(dir, "asc", StringComparison.OrdinalIgnoreCase);
        query = sort switch
        {
            "title" => ascending ? query.OrderBy(e => e.Title) : query.OrderByDescending(e => e.Title),
            "price" => ascending ? query.OrderBy(e => e.TicketPrice) : query.OrderByDescending(e => e.TicketPrice),
            _ => ascending ? query.OrderBy(e => e.EventDate) : query.OrderByDescending(e => e.EventDate)
        };

        var events = await query.ToListAsync();
        return PartialView("_EventTable", events);
    }

    [HttpGet]
    public async Task<IActionResult> Overview()
    {
        var events = await _context.Events.Include(e => e.Category).ToListAsync();
        ViewBag.TotalEvents = events.Count;
        ViewBag.TotalCategories = await _context.Categories.CountAsync();
        ViewBag.LowTicketEvents = events.Count(e => e.AvailableTickets < 5);
        return View(events);
    }

    [Authorize(Roles = "Organizer,Admin")]
    [HttpGet]
    public async Task<IActionResult> MyAnalytics()
    {
        return View();
    }

    [Authorize(Roles = "Organizer,Admin")]
    [HttpGet]
    public async Task<IActionResult> GetAnalyticsData()
    {
        var user = await _userManager.GetUserAsync(User);
        var isAdmin = user != null && await _userManager.IsInRoleAsync(user, "Admin");
        
        // Chart 1: Ticket sales by category
        var ticketsQuery = _context.Tickets
            .Include(t => t.Event)
            .ThenInclude(e => e.Category)
            .AsQueryable();

        // Filter by organizer if not admin
        if (!isAdmin && user != null)
        {
            ticketsQuery = ticketsQuery.Where(t => t.Event.OrganizerId == user.Id);
        }

        var salesByCategory = await ticketsQuery
            .GroupBy(t => t.Event.Category.Name)
            .Select(g => new
            {
                Category = g.Key ?? "Uncategorized",
                TicketCount = g.Count()
            })
            .ToListAsync();

        // Chart 2: Revenue per month
        var purchasesQuery = _context.Purchases
            .Include(p => p.Tickets)
            .ThenInclude(t => t.Event)
            .AsQueryable();

        // Filter by organizer if not admin
        if (!isAdmin && user != null)
        {
            purchasesQuery = purchasesQuery.Where(p => p.Tickets.Any(t => t.Event.OrganizerId == user.Id));
        }

        // Get revenue data and format on client side (can't use ToString in LINQ to SQL)
        var revenueByMonthData = await purchasesQuery
            .GroupBy(p => new { Year = p.PurchaseDate.Year, Month = p.PurchaseDate.Month })
            .Select(g => new
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Revenue = g.Sum(p => p.TotalCost)
            })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync();

        // Format dates after materializing the query
        var revenueByMonth = revenueByMonthData.Select(x => new
        {
            Month = new DateTime(x.Year, x.Month, 1).ToString("yyyy-MM"),
            MonthName = new DateTime(x.Year, x.Month, 1).ToString("MMMM yyyy"),
            Revenue = x.Revenue
        }).ToList();

        // Top 5 Best-Selling Events
        var topEventsQuery = _context.Tickets
            .Include(t => t.Event)
            .AsQueryable();

        if (!isAdmin && user != null)
        {
            topEventsQuery = topEventsQuery.Where(t => t.Event.OrganizerId == user.Id);
        }

        var topEvents = await topEventsQuery
            .GroupBy(t => new { t.EventId, t.Event.Title })
            .Select(g => new
            {
                EventId = g.Key.EventId,
                EventTitle = g.Key.Title,
                TicketsSold = g.Count(),
                Revenue = g.Sum(t => t.Price)
            })
            .OrderByDescending(x => x.TicketsSold)
            .Take(5)
            .ToListAsync();

        return Json(new
        {
            salesByCategory = salesByCategory,
            revenueByMonth = revenueByMonth,
            topEvents = topEvents
        });
    }
}
