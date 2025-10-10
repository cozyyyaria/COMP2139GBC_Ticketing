# PowerShell script to clear purchase history
$connectionString = "Host=localhost;Database=gbc_ticketing;Username=postgres;Password=postgres"

# Create a simple C# program to clear the data
$csharpCode = @"
using Microsoft.EntityFrameworkCore;
using GBC_Ticketing.Web.Models;

var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
optionsBuilder.UseNpgsql("$connectionString");

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
"@

# Write the C# code to a temporary file
$csharpCode | Out-File -FilePath "temp_clear.cs" -Encoding UTF8

# Compile and run
dotnet run --project . -- temp_clear.cs

# Clean up
Remove-Item "temp_clear.cs" -ErrorAction SilentlyContinue
