using Microsoft.AspNetCore.Mvc;
using GBC_Ticketing.Web.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace GBC_Ticketing.Web.Controllers
{
    public class PurchaseController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PurchaseController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Purchase/Buy/5
        public async Task<IActionResult> Buy(int id)
        {
            var ev = await _context.Events.FindAsync(id);
            if (ev == null) 
            {
                Console.WriteLine($"Event with ID {id} not found");
                return NotFound();
            }

            Console.WriteLine($"Loading event {id}: Title={ev.Title}, Price={ev.TicketPrice}, Available={ev.AvailableTickets}");

            var model = new PurchaseViewModel
            {
                EventId = ev.Id,
                EventTitle = ev.Title,
                AvailableTickets = ev.AvailableTickets,
                TicketPrice = ev.TicketPrice
            };

            Console.WriteLine($"Created PurchaseViewModel: EventId={model.EventId}, EventTitle={model.EventTitle}, AvailableTickets={model.AvailableTickets}, TicketPrice={model.TicketPrice}");

            return View(model);
        }

        // POST: Purchase/Buy
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Buy(PurchaseViewModel model)
        {
            // Debug: Log the received model
            Console.WriteLine($"Received Purchase: EventId={model.EventId}, GuestName={model.GuestName}, GuestEmail={model.GuestEmail}, Quantity={model.Quantity}");
            
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
            
            if (!ModelState.IsValid) return View(model);

            var ev = await _context.Events.FindAsync(model.EventId);
            if (ev == null) return NotFound();

            if (model.Quantity > ev.AvailableTickets)
            {
                ModelState.AddModelError("", "Not enough tickets available.");
                return View(model);
            }

            try
            {
                // Create Purchase
                var purchase = new Purchase
                {
                    GuestName = model.GuestName,
                    GuestEmail = model.GuestEmail,
                    PurchaseDate = DateTime.UtcNow, // Use UTC for PostgreSQL compatibility
                    TotalCost = model.Quantity * ev.TicketPrice,
                    Tickets = Enumerable.Range(1, model.Quantity)
                                        .Select(i => new Ticket { EventId = ev.Id, Price = ev.TicketPrice })
                                        .ToList()
                };

                _context.Purchases.Add(purchase);

                // Update event ticket availability
                ev.AvailableTickets -= model.Quantity;
                Console.WriteLine($"Updated event {ev.Id}: AvailableTickets = {ev.AvailableTickets}");

                await _context.SaveChangesAsync();
                Console.WriteLine("Purchase saved successfully!");

                // Redirect to Confirmation page
                return RedirectToAction("Confirmation", new { purchaseId = purchase.Id });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during purchase: {ex.Message}");
                ModelState.AddModelError("", "An error occurred while processing your purchase: " + ex.Message);
                return View(model);
            }
        }

        // GET: Purchase/Confirmation/5
        public async Task<IActionResult> Confirmation(int purchaseId)
        {
            var purchase = await _context.Purchases
                .Include(p => p.Tickets)
                .ThenInclude(t => t.Event)
                .FirstOrDefaultAsync(p => p.Id == purchaseId);

            if (purchase == null) return NotFound();

            return View(purchase);
        }

        // GET: Purchase/Index
        public async Task<IActionResult> Index()
        {
            var purchases = await _context.Purchases
                .Include(p => p.Tickets)
                .ThenInclude(t => t.Event)
                .ToListAsync();

            return View(purchases);
        }

        // GET: Purchase/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var purchase = await _context.Purchases
                .Include(p => p.Tickets)
                .ThenInclude(t => t.Event)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (purchase == null)
            {
                return NotFound();
            }

            return View(purchase);
        }
    }

    // ViewModel for purchasing tickets
    public class PurchaseViewModel
    {
        public int EventId { get; set; }
        public string EventTitle { get; set; } = string.Empty;
        public int AvailableTickets { get; set; }
        public decimal TicketPrice { get; set; }
        
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int Quantity { get; set; }

        [Required]
        public string GuestName { get; set; } = string.Empty;
        
        [Required]
        [EmailAddress]
        public string GuestEmail { get; set; } = string.Empty;
    }
}


