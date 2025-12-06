using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Http;

namespace GBC_Ticketing.Web.Models;

public class Event
{
    public int Id { get; set; }
    
    [Required]
    public string Title { get; set; } = string.Empty;
    
    [Required]
    public int CategoryId { get; set; }
    
    public Category? Category { get; set; }
    
    [Required]
    public DateTime EventDate { get; set; }
    
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Ticket price must be greater than 0")]
    public decimal TicketPrice { get; set; }
    
    [Required]
    [Range(0, int.MaxValue, ErrorMessage = "Available tickets must be 0 or greater")]
    public int AvailableTickets { get; set; }
    
    public string? OrganizerId { get; set; }
    public ApplicationUser? Organizer { get; set; }
    
    public string? ImagePath { get; set; }
    
    [NotMapped]
    public IFormFile? ImageFile { get; set; }
    
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
