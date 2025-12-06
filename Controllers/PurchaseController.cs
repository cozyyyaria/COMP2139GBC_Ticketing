using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using GBC_Ticketing.Web.Models;
using GBC_Ticketing.Web.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Serilog;
using System.Data.Common;
using System.Runtime.CompilerServices;

namespace GBC_Ticketing.Web.Controllers
{
    public class PurchaseController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;

        public PurchaseController(ApplicationDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
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

        [HttpGet]
        public async Task<IActionResult> GetCartInfo(int eventId, int quantity)
        {
            var ev = await _context.Events.FindAsync(eventId);
            if (ev == null)
            {
                return Json(new { error = "Event not found" });
            }

            var totalPrice = quantity * ev.TicketPrice;
            var isLowStock = ev.AvailableTickets < 5 && ev.AvailableTickets > 0;
            var isSoldOut = ev.AvailableTickets == 0;
            var canPurchase = quantity > 0 && quantity <= ev.AvailableTickets;

            return Json(new
            {
                quantity = quantity,
                totalPrice = totalPrice,
                availableTickets = ev.AvailableTickets,
                isLowStock = isLowStock,
                isSoldOut = isSoldOut,
                canPurchase = canPurchase,
                lowStockMessage = isLowStock ? $"Only {ev.AvailableTickets} tickets left!" : null
            });
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
                var entry = ModelState[key];
                if (entry != null && entry.Errors != null)
                {
                    foreach (var error in entry.Errors)
                    {
                        Console.WriteLine($"ModelState Error - Key: {key}, Error: {error.ErrorMessage}");
                    }
                }
            }
            
            if (!ModelState.IsValid) 
                return View(model);

            var ev = await _context.Events.FindAsync(model.EventId);
            if (ev == null) 
                return NotFound();

            if (model.Quantity > ev.AvailableTickets)
            {
                ModelState.AddModelError("", "Not enough tickets available.");
                return View(model);
            }

            try
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                
                // Get current user if logged in
                string? userId = null;
                if (User.Identity?.IsAuthenticated == true)
                {
                    var userManager = HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                    var user = await userManager.GetUserAsync(User);
                    userId = user?.Id;
                }
                
                // COMPLETELY bypass EF Core - use ADO.NET directly
                var purchaseDate = DateTime.UtcNow;
                var totalCost = model.Quantity * ev.TicketPrice;
                var guestName = model.GuestName.Trim();
                var guestEmail = model.GuestEmail.Trim();
                
                // Get the database connection
                var connection = _context.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                {
                    await connection.OpenAsync();
                }
                
                int purchaseId = 0;
                
                // Insert Purchase using ADO.NET with Rating = 0
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT INTO ""Purchases"" (""GuestName"", ""GuestEmail"", ""PurchaseDate"", ""TotalCost"", ""UserId"", ""Rating"")
                        VALUES (@guestName, @guestEmail, @purchaseDate, @totalCost, @userId, 0)
                        RETURNING ""Id""
                    ";
                    
                    var pGuestName = cmd.CreateParameter();
                    pGuestName.ParameterName = "@guestName";
                    pGuestName.Value = guestName;
                    cmd.Parameters.Add(pGuestName);
                    
                    var pGuestEmail = cmd.CreateParameter();
                    pGuestEmail.ParameterName = "@guestEmail";
                    pGuestEmail.Value = guestEmail;
                    cmd.Parameters.Add(pGuestEmail);
                    
                    var pPurchaseDate = cmd.CreateParameter();
                    pPurchaseDate.ParameterName = "@purchaseDate";
                    pPurchaseDate.Value = purchaseDate;
                    cmd.Parameters.Add(pPurchaseDate);
                    
                    var pTotalCost = cmd.CreateParameter();
                    pTotalCost.ParameterName = "@totalCost";
                    pTotalCost.Value = totalCost;
                    cmd.Parameters.Add(pTotalCost);
                    
                    var pUserId = cmd.CreateParameter();
                    pUserId.ParameterName = "@userId";
                    pUserId.Value = (object?)userId ?? DBNull.Value;
                    cmd.Parameters.Add(pUserId);
                    
                    var result = await cmd.ExecuteScalarAsync();
                    purchaseId = Convert.ToInt32(result);
                }
                
                if (purchaseId == 0)
                {
                    throw new Exception("Failed to create purchase - no ID returned from database.");
                }
                
                Console.WriteLine($"Purchase inserted via ADO.NET with ID: {purchaseId}, Rating = 0");
                
                // Create a simple Purchase object for reference (not tracked by EF)
                var purchase = new Purchase { Id = purchaseId, TotalCost = totalCost };
                
                // Insert tickets using ADO.NET
                for (int i = 1; i <= model.Quantity; i++)
                {
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = @"
                            INSERT INTO ""Tickets"" (""PurchaseId"", ""EventId"", ""Price"", ""SeatNumber"")
                            VALUES (@purchaseId, @eventId, @price, @seatNumber)
                        ";
                        
                        var pPurchaseId = cmd.CreateParameter();
                        pPurchaseId.ParameterName = "@purchaseId";
                        pPurchaseId.Value = purchaseId;
                        cmd.Parameters.Add(pPurchaseId);
                        
                        var pEventId = cmd.CreateParameter();
                        pEventId.ParameterName = "@eventId";
                        pEventId.Value = ev.Id;
                        cmd.Parameters.Add(pEventId);
                        
                        var pPrice = cmd.CreateParameter();
                        pPrice.ParameterName = "@price";
                        pPrice.Value = ev.TicketPrice;
                        cmd.Parameters.Add(pPrice);
                        
                        var pSeatNumber = cmd.CreateParameter();
                        pSeatNumber.ParameterName = "@seatNumber";
                        pSeatNumber.Value = $"SEAT-{purchaseId}-{i}";
                        cmd.Parameters.Add(pSeatNumber);
                        
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                Console.WriteLine($"Inserted {model.Quantity} tickets via ADO.NET for Purchase {purchaseId}");

                // Update event ticket availability using ADO.NET
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = @"
                        UPDATE ""Events"" 
                        SET ""AvailableTickets"" = @newAvailable
                        WHERE ""Id"" = @eventId
                    ";
                    
                    var pNewAvailable = cmd.CreateParameter();
                    pNewAvailable.ParameterName = "@newAvailable";
                    pNewAvailable.Value = ev.AvailableTickets - model.Quantity;
                    cmd.Parameters.Add(pNewAvailable);
                    
                    var pEventId = cmd.CreateParameter();
                    pEventId.ParameterName = "@eventId";
                    pEventId.Value = ev.Id;
                    cmd.Parameters.Add(pEventId);
                    
                    await cmd.ExecuteNonQueryAsync();
                }
                Console.WriteLine($"Updated event {ev.Id}: AvailableTickets = {ev.AvailableTickets - model.Quantity}");
                
                Console.WriteLine("Purchase and tickets saved successfully!");

                // Log successful purchase
                Log.Information(
                    "Purchase completed - Guest: {GuestEmail}, EventId: {EventId}, Quantity: {Quantity}, Total: {Total}, IP: {IP}", 
                    model.GuestEmail, ev.Id, model.Quantity, purchase.TotalCost, ipAddress);

                // Try to send confirmation email (don't fail purchase if email breaks)
                try
                {
                    var emailBody = $@"
                        <h2>Thank you for your purchase, {model.GuestName}!</h2>
                        <p>You bought <strong>{model.Quantity}</strong> ticket(s) for <strong>{ev.Title}</strong>.</p>
                        <p>Total paid: <strong>{purchase.TotalCost:C}</strong></p>
                        <p>Event date: {ev.EventDate:MMMM dd, yyyy}</p>
                        <p>We look forward to seeing you!</p>
                    ";

                    await _emailService.SendEmailAsync(
                        model.GuestEmail,
                        $"Ticket Purchase Confirmation - {ev.Title}",
                        emailBody
                    );

                    Log.Information("Confirmation email sent to {Email} for purchase {PurchaseId}", model.GuestEmail, purchase.Id);
                }
                catch (Exception emailEx)
                {
                    Log.Error(emailEx, "Failed to send confirmation email to {Email} for purchase {PurchaseId}", model.GuestEmail, purchase.Id);
                }

                // Redirect to Confirmation page
                return RedirectToAction("Confirmation", new { purchaseId = purchase.Id });
            }
            catch (DbUpdateException dbEx)
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                
                // Get the innermost exception
                var innerEx = dbEx.InnerException;
                while (innerEx?.InnerException != null)
                {
                    innerEx = innerEx.InnerException;
                }
                
                var errorMessage = dbEx.Message;
                var detailedError = errorMessage;
                
                if (innerEx != null)
                {
                    detailedError = innerEx.Message;
                    Console.WriteLine($"=== DATABASE ERROR ===");
                    Console.WriteLine($"Inner Exception: {innerEx.Message}");
                    Console.WriteLine($"Inner Exception Type: {innerEx.GetType().Name}");
                    if (innerEx is Npgsql.PostgresException pgEx)
                    {
                        Console.WriteLine($"PostgreSQL Error Code: {pgEx.SqlState}");
                        Console.WriteLine($"PostgreSQL Error Detail: {pgEx.Detail}");
                        detailedError = $"{pgEx.MessageText} (Code: {pgEx.SqlState})";
                    }
                    Console.WriteLine($"Full Stack Trace:");
                    Console.WriteLine(innerEx.StackTrace);
                }
                
                Log.Error(dbEx, "Purchase failed (DbUpdateException) - Guest: {GuestEmail}, EventId: {EventId}, IP: {IP}, Error: {Error}", 
                    model.GuestEmail, model.EventId, ipAddress, detailedError);
                Console.WriteLine($"=== PURCHASE ERROR ===");
                Console.WriteLine($"Error: {detailedError}");
                Console.WriteLine($"Stack Trace: {dbEx.StackTrace}");
                
                // Show user-friendly error message
                var userFriendlyMessage = "An error occurred while processing your purchase.";
                if (innerEx != null && innerEx.Message.Contains("Rating"))
                {
                    userFriendlyMessage += " There was a database error with the rating field. Please try again.";
                }
                else if (innerEx != null)
                {
                    userFriendlyMessage += " " + detailedError;
                }
                
                ModelState.AddModelError("", userFriendlyMessage);
                TempData["Error"] = userFriendlyMessage;
                return View(model);
            }
            catch (Exception ex)
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                
                // Log full exception details
                var errorMessage = ex.Message;
                if (ex.InnerException != null)
                {
                    errorMessage += " | Inner: " + ex.InnerException.Message;
                    Console.WriteLine($"=== GENERAL ERROR ===");
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                    Console.WriteLine($"Stack Trace: {ex.InnerException.StackTrace}");
                }
                
                Log.Error(ex, "Purchase failed - Guest: {GuestEmail}, EventId: {EventId}, IP: {IP}, Error: {Error}", 
                    model.GuestEmail, model.EventId, ipAddress, errorMessage);
                Console.WriteLine($"Error during purchase: {errorMessage}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                
                ModelState.AddModelError("", "An error occurred while processing your purchase: " + errorMessage);
                TempData["Error"] = "An error occurred while processing your purchase: " + errorMessage;
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

            if (purchase == null) 
                return NotFound();

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
