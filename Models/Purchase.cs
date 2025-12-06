using System.ComponentModel.DataAnnotations;

namespace GBC_Ticketing.Web.Models;

public class Purchase
{
    public int Id { get; set; }
    public DateTime PurchaseDate { get; set; }
    
    [Required]
    public string GuestName { get; set; } = string.Empty;
    
    [Required]
    [EmailAddress]
    public string GuestEmail { get; set; } = string.Empty;
    
    public decimal TotalCost { get; set; }
    
    // User relationship
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    
    // Rating for the event (1-5 stars). Defaults to 0 (not rated).
    // Non-nullable with default value of 0
    public int Rating { get; set; } = 0;
    
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
