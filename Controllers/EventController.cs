using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GBC_Ticketing.Web.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GBC_Ticketing.Web.Controllers;

public class EventController : Controller
{
    private readonly ApplicationDbContext _context;
    public EventController(ApplicationDbContext context) => _context = context;


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
            query = query.Where(e => e.EventDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(e => e.EventDate <= endDate.Value);
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

        ViewBag.TotalEvents = events.Count;
        ViewBag.TotalCategories = await _context.Categories.CountAsync();
        ViewBag.LowTicketEvents = events.Count(e => e.AvailableTickets < 5);
        ViewBag.Categories = new SelectList(await _context.Categories.ToListAsync(), "Id", "Name");

        return View(events);
    }


    [HttpGet]
    public IActionResult Create()
    {
        ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name");
        return View();
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Event @event)
    {
        // Debug: Log the received model
        Console.WriteLine($"Received Event: Title={@event.Title}, CategoryId={@event.CategoryId}, EventDate={@event.EventDate}, TicketPrice={@event.TicketPrice}, AvailableTickets={@event.AvailableTickets}");
        
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


    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var @event = await _context.Events.FindAsync(id);
        if (@event == null)
        {
            return NotFound(); // Missing 'return' and semicolon
        }
        ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", @event.CategoryId);
        return View(@event);
    }
    
    

    [HttpPost]
    public async Task<IActionResult> Edit(Event e)
    {
        if (@e.EventDate != default && @e.EventDate.Kind == DateTimeKind.Unspecified)
        {
            @e.EventDate = DateTime.SpecifyKind(@e.EventDate, DateTimeKind.Utc);
        }
       
        if (ModelState.IsValid)
        {
            _context.Update(e);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
      
        return View(e);
    }

    // GET: Event/Delete/5
    public async Task<IActionResult> Delete(int id)
    {
        var ev = await _context.Events
            .FirstOrDefaultAsync(e => e.Id == id);
        if (ev == null)
        {
            return NotFound();
        }
        return View(ev);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        try
        {
            var ev = await _context.Events.FindAsync(id);
            if (ev == null)
            {
                return NotFound();
            }
            
            Console.WriteLine($"Deleting event {id}: {ev.Title}");
            _context.Events.Remove(ev);
            await _context.SaveChangesAsync();
            Console.WriteLine("Event deleted successfully!");
            
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting event: {ex.Message}");
            return RedirectToAction(nameof(Index));
        }
    }

    // GET: Event/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var ev = await _context.Events.Include(e => e.Category).FirstOrDefaultAsync(e => e.Id == id);
        return ev == null ? NotFound() : View(ev);
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
            query = query.Where(e => e.EventDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(e => e.EventDate <= endDate.Value);
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
}
