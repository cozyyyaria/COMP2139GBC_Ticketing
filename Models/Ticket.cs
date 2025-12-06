using System.ComponentModel.DataAnnotations;

namespace GBC_Ticketing.Web.Models
{
    public class Ticket
    {
        public int Id { get; set; }
        public string SeatNumber { get; set; } = string.Empty;
        public int PurchaseId { get; set; }
        public Purchase Purchase { get; set; } = null!;
        public int EventId { get; set; }
        public Event Event { get; set; } = null!;
        public decimal Price { get; set; }
    }
}