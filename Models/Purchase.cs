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
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
