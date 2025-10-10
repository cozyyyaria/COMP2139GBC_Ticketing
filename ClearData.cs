using Microsoft.EntityFrameworkCore;
using GBC_Ticketing.Web.Models;

namespace GBC_Ticketing.Web
{
    public class ClearData
    {
        public static async Task Main(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseNpgsql("Host=localhost;Database=gbc_ticketing;Username=postgres;Password=postgres");

            using var context = new ApplicationDbContext(optionsBuilder.Options);

            Console.WriteLine("Clearing all purchase history...");

            // Delete all tickets first (due to foreign key constraints)
            var tickets = await context.Tickets.ToListAsync();
            context.Tickets.RemoveRange(tickets);
            Console.WriteLine($"Deleted {tickets.Count} tickets");

            // Delete all purchases
            var purchases = await context.Purchases.ToListAsync();
            context.Purchases.RemoveRange(purchases);
            Console.WriteLine($"Deleted {purchases.Count} purchases");

            await context.SaveChangesAsync();
            Console.WriteLine("Purchase history cleared successfully!");
        }
    }
}
