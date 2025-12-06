using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GBC_Ticketing.Web.Models;
using QRCoder;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace GBC_Ticketing.Web.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public DashboardController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!User.Identity?.IsAuthenticated ?? true)
            {
                return RedirectToAction("Login", "Account");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Check roles in order of priority
            if (await _userManager.IsInRoleAsync(user, "Admin"))
            {
                return RedirectToAction("Admin");
            }
            
            if (await _userManager.IsInRoleAsync(user, "Organizer"))
            {
                return RedirectToAction("Organizer");
            }
            
            if (await _userManager.IsInRoleAsync(user, "Attendee"))
            {
                return RedirectToAction("Attendee");
            }

            // User has no role - assign Attendee as default
            try
            {
                var result = await _userManager.AddToRoleAsync(user, "Attendee");
                if (result.Succeeded)
                {
                    return RedirectToAction("Attendee");
                }
            }
            catch
            {
                // Role might not exist, continue to error
            }
            
            TempData["Error"] = "Your account does not have a role assigned. Please contact an administrator.";
            return RedirectToAction("Login", "Account");
        }

        [Authorize(Roles = "Attendee")]
        [HttpGet]
        public async Task<IActionResult> Attendee()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var now = DateTime.UtcNow;
            var userEmail = (user.Email ?? string.Empty).ToLower();

            // Get ALL purchases (by UserId or email)
            var allPurchases = await _context.Purchases
                .Include(p => p.Tickets)
                    .ThenInclude(t => t.Event)
                        .ThenInclude(e => e.Category)
                .Where(p => p.UserId == user.Id ||
                            (p.GuestEmail != null && p.GuestEmail.ToLower() == userEmail))
                .OrderByDescending(p => p.PurchaseDate)
                .ToListAsync();

            // Filter for upcoming events (where Event is not null and date is in future)
            var upcomingPurchases = allPurchases
                .Where(p => p.Tickets.Any(t => t.Event != null && t.Event.EventDate > now))
                .ToList();

            // Filter for past events (where Event is not null and date is in past)
            var pastPurchases = allPurchases
                .Where(p => p.Tickets.Any(t => t.Event != null && t.Event.EventDate <= now))
                .ToList();

            ViewBag.UpcomingPurchases = upcomingPurchases;
            ViewBag.PastPurchases = pastPurchases;
            ViewBag.User = user;

            return View();
        }

        [Authorize(Roles = "Organizer")]
        [HttpGet]
        public async Task<IActionResult> Organizer()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var myEvents = await _context.Events
                .Include(e => e.Category)
                .Include(e => e.Tickets)
                .Where(e => e.OrganizerId == user.Id)
                .OrderByDescending(e => e.EventDate)
                .ToListAsync();

            var eventsWithRevenue = myEvents.Select(e => new
            {
                Event = e,
                Revenue = e.Tickets.Sum(t => t.Price),
                TicketsSold = e.Tickets.Count
            }).ToList();

            ViewBag.EventsWithRevenue = eventsWithRevenue;
            ViewBag.User = user;

            return View();
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Admin()
        {
            var allEvents = await _context.Events
                .Include(e => e.Organizer)
                .Include(e => e.Category)
                .Include(e => e.Tickets)
                .OrderByDescending(e => e.EventDate)
                .ToListAsync();

            var organizers = await _userManager.GetUsersInRoleAsync("Organizer");
            var organizerStats = organizers.Select(o => new
            {
                Organizer = o,
                Events = allEvents.Where(e => e.OrganizerId == o.Id).ToList(),
                TotalRevenue = allEvents.Where(e => e.OrganizerId == o.Id).Sum(e => e.Tickets.Sum(t => t.Price))
            }).ToList();

            ViewBag.OrganizerStats = organizerStats;
            ViewBag.AllEvents = allEvents;

            return View();
        }

        [Authorize(Roles = "Attendee,Admin")]
        [HttpGet]
        public async Task<IActionResult> MyTickets()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var userEmail = (user.Email ?? string.Empty).ToLower();

            // Get ALL purchases where:
            // 1. UserId matches (logged-in purchases), OR
            // 2. GuestEmail matches user's email (guest purchases made with same email)
            var allPurchases = await _context.Purchases
                .Where(p => p.UserId == user.Id ||
                            (p.GuestEmail != null && p.GuestEmail.ToLower() == userEmail))
                .OrderByDescending(p => p.PurchaseDate)
                .ToListAsync();

            // Query ALL tickets for these purchases directly (more reliable than Include)
            var purchaseIds = allPurchases.Select(p => p.Id).ToList();
            var allTickets = await _context.Tickets
                .Include(t => t.Event)
                    .ThenInclude(e => e.Category)
                .Where(t => purchaseIds.Contains(t.PurchaseId))
                .ToListAsync();

            // Manually attach tickets to purchases
            foreach (var purchase in allPurchases)
            {
                var ticketsForPurchase = allTickets.Where(t => t.PurchaseId == purchase.Id).ToList();
                purchase.Tickets = ticketsForPurchase;
            }
            
            // Debug: Log detailed information
            Console.WriteLine($"=== MY TICKETS DEBUG ===");
            Console.WriteLine($"User ID: {user.Id}");
            Console.WriteLine($"User Email: {user.Email}");
            Console.WriteLine($"Found {allPurchases.Count} purchases");
            Console.WriteLine($"Found {allTickets.Count} total tickets");
            foreach (var purchase in allPurchases)
            {
                Console.WriteLine($"Purchase {purchase.Id}: {purchase.Tickets.Count} tickets, Email: {purchase.GuestEmail}, UserId: {purchase.UserId}");
                foreach (var ticket in purchase.Tickets)
                {
                    Console.WriteLine($"  - Ticket {ticket.Id}: EventId={ticket.EventId}, Event={(ticket.Event != null ? ticket.Event.Title : "NULL")}, EventDate={(ticket.Event != null ? ticket.Event.EventDate.ToString() : "NULL")}");
                }
            }

            // Separate upcoming and past tickets
            var now = DateTime.UtcNow;
            
            // Get ALL purchases with tickets (both upcoming and past) for display
            var purchasesWithTickets = allPurchases
                .Where(p => p.Tickets != null && p.Tickets.Any(t => t.Event != null))
                .ToList();

            // Debug info - count all purchases (by UserId or email)
            var totalPurchases = allPurchases.Count;
            var totalTickets = allPurchases.Sum(p => p.Tickets?.Count ?? 0);
            var upcomingTicketsCount = allPurchases
                .SelectMany(p => p.Tickets ?? new List<Ticket>())
                .Count(t => t.Event != null && t.Event.EventDate > now);
            var pastTicketsCount = allPurchases
                .SelectMany(p => p.Tickets ?? new List<Ticket>())
                .Count(t => t.Event != null && t.Event.EventDate <= now);
            var upcomingEvents = await _context.Events.Where(e => e.EventDate > now).CountAsync();
            
            ViewBag.TotalPurchases = totalPurchases;
            ViewBag.TotalTickets = totalTickets;
            ViewBag.UpcomingTicketsCount = upcomingTicketsCount;
            ViewBag.PastTicketsCount = pastTicketsCount;
            ViewBag.UpcomingEvents = upcomingEvents;
            ViewBag.UserId = user.Id;
            ViewBag.UserEmail = user.Email;
            ViewBag.AllPurchases = allPurchases; // Pass all purchases for debugging

            // Return ALL purchases with tickets (we'll separate them in the view)
            return View(purchasesWithTickets);
        }

        [Authorize(Roles = "Attendee,Admin")]
        [HttpGet]
        public async Task<IActionResult> PurchaseHistory()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var userEmail = (user.Email ?? string.Empty).ToLower();

            // Get purchases where:
            // 1. UserId matches (logged-in purchases), OR
            // 2. GuestEmail matches user's email (guest purchases made with same email)
            var purchases = await _context.Purchases
                .Include(p => p.Tickets)
                    .ThenInclude(t => t.Event)
                .Where(p => p.UserId == user.Id ||
                            (p.GuestEmail != null && p.GuestEmail.ToLower() == userEmail))
                .OrderByDescending(p => p.PurchaseDate)
                .ToListAsync();

            return View(purchases);
        }

        [Authorize(Roles = "Attendee,Admin")]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> UpdateRating([FromBody] UpdateRatingRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var purchase = await _context.Purchases
                .FirstOrDefaultAsync(p => p.Id == request.PurchaseId && p.UserId == user.Id);

            if (purchase == null) return NotFound();

            if (request.Rating < 1 || request.Rating > 5)
            {
                return BadRequest(new { success = false, message = "Rating must be between 1 and 5" });
            }

            purchase.Rating = request.Rating;
            await _context.SaveChangesAsync();

            return Json(new { success = true, rating = request.Rating });
        }

        [Authorize(Roles = "Attendee,Admin")]
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            return View(user);
        }

        [Authorize(Roles = "Attendee,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ProfileViewModel model, IFormFile? profileImage)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            if (ModelState.IsValid)
            {
                user.FullName = model.FullName;
                user.PhoneNumber = model.PhoneNumber;

                // Handle profile image upload
                if (profileImage != null && profileImage.Length > 0)
                {
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "profiles");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var uniqueFileName = $"{user.Id}_{Guid.NewGuid()}{Path.GetExtension(profileImage.FileName)}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await profileImage.CopyToAsync(stream);
                    }

                    // Delete old profile image if exists
                    if (!string.IsNullOrEmpty(user.ProfileImagePath))
                    {
                        var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", user.ProfileImagePath.TrimStart('/'));
                        if (System.IO.File.Exists(oldFilePath))
                        {
                            System.IO.File.Delete(oldFilePath);
                        }
                    }

                    user.ProfileImagePath = $"/uploads/profiles/{uniqueFileName}";
                }

                var result = await _userManager.UpdateAsync(user);
                if (result.Succeeded)
                {
                    TempData["Message"] = "Profile updated successfully!";
                    return RedirectToAction("Profile");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return View(user);
        }

        [Authorize(Roles = "Attendee,Admin")]
        [HttpGet]
        public async Task<IActionResult> GenerateQRCode(int ticketId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var userEmail = (user.Email ?? string.Empty).ToLower();

            var ticket = await _context.Tickets
                .Include(t => t.Event)
                .Include(t => t.Purchase)
                .FirstOrDefaultAsync(t => t.Id == ticketId &&
                    (t.Purchase.UserId == user.Id ||
                     (t.Purchase.GuestEmail != null && t.Purchase.GuestEmail.ToLower() == userEmail)));

            if (ticket == null) return NotFound();

            // Generate QR code data
            var qrData = $"TicketID:{ticket.Id}|EventID:{ticket.EventId}|PurchaseID:{ticket.PurchaseId}|Validated:False";
            
            using (var qrGenerator = new QRCodeGenerator())
            {
                var qrCodeData = qrGenerator.CreateQrCode(qrData, QRCodeGenerator.ECCLevel.Q);
                using (var qrCode = new PngByteQRCode(qrCodeData))
                {
                    var qrCodeBytes = qrCode.GetGraphic(20);
                    return File(qrCodeBytes, "image/png");
                }
            }
        }

        [Authorize(Roles = "Attendee,Admin")]
        [HttpGet]
        public async Task<IActionResult> DownloadTicketPDF(int purchaseId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var userEmail = (user.Email ?? string.Empty).ToLower();

            var purchase = await _context.Purchases
                .Include(p => p.Tickets)
                    .ThenInclude(t => t.Event)
                .FirstOrDefaultAsync(p => p.Id == purchaseId &&
                    (p.UserId == user.Id ||
                     (p.GuestEmail != null && p.GuestEmail.ToLower() == userEmail)));

            if (purchase == null) return NotFound();

            // Generate simple HTML ticket (can be enhanced with PDF library)
            var html = $@"
                <html>
                <head><title>Ticket - {purchase.Tickets.First().Event.Title}</title></head>
                <body>
                    <h1>GBC Ticketing System</h1>
                    <h2>{purchase.Tickets.First().Event.Title}</h2>
                    <p><strong>Guest Name:</strong> {purchase.GuestName}</p>
                    <p><strong>Email:</strong> {purchase.GuestEmail}</p>
                    <p><strong>Event Date:</strong> {purchase.Tickets.First().Event.EventDate:MMMM dd, yyyy HH:mm}</p>
                    <p><strong>Number of Tickets:</strong> {purchase.Tickets.Count}</p>
                    <p><strong>Total Cost:</strong> ${purchase.TotalCost:F2}</p>
                    <p><strong>Purchase Date:</strong> {purchase.PurchaseDate:MMMM dd, yyyy}</p>
                </body>
                </html>
            ";

            var bytes = Encoding.UTF8.GetBytes(html);
            return File(bytes, "text/html", $"ticket_{purchaseId}.html");
        }
    }

    public class ProfileViewModel
    {
        [Required]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Phone]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }
    }

    public class UpdateRatingRequest
    {
        public int PurchaseId { get; set; }
        public int Rating { get; set; }
    }
}
