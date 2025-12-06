using Xunit;
using System.ComponentModel.DataAnnotations;
using GBC_Ticketing.Web.Models;

namespace WebApplication1.Tests;

public class EventModelTests
{
    [Fact]
    public void Event_ShouldHaveRequiredTitle()
    {
        // Arrange
        var @event = new Event();
        var validationContext = new ValidationContext(@event);

        // Act
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(@event, validationContext, results, true);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains("Title"));
    }

    [Fact]
    public void Event_ShouldHaveValidTicketPrice()
    {
        // Arrange
        var @event = new Event
        {
            Title = "Test Event",
            CategoryId = 1,
            EventDate = DateTime.UtcNow.AddDays(30),
            TicketPrice = 0, // Invalid: must be > 0
            AvailableTickets = 10
        };
        var validationContext = new ValidationContext(@event);

        // Act
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(@event, validationContext, results, true);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains("TicketPrice"));
    }

    [Fact]
    public void Event_ShouldHaveValidAvailableTickets()
    {
        // Arrange
        var @event = new Event
        {
            Title = "Test Event",
            CategoryId = 1,
            EventDate = DateTime.UtcNow.AddDays(30),
            TicketPrice = 50.00m,
            AvailableTickets = -1 // Invalid: must be >= 0
        };
        var validationContext = new ValidationContext(@event);

        // Act
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(@event, validationContext, results, true);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains("AvailableTickets"));
    }

    [Fact]
    public void Event_WithValidData_ShouldPassValidation()
    {
        // Arrange
        var @event = new Event
        {
            Title = "Test Event",
            CategoryId = 1,
            EventDate = DateTime.UtcNow.AddDays(30),
            TicketPrice = 50.00m,
            AvailableTickets = 100
        };
        var validationContext = new ValidationContext(@event);

        // Act
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(@event, validationContext, results, true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(results);
    }
}


