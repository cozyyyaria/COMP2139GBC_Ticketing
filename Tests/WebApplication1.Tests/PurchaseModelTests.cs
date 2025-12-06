using Xunit;
using System.ComponentModel.DataAnnotations;
using GBC_Ticketing.Web.Models;

namespace WebApplication1.Tests;

public class PurchaseModelTests
{
    [Fact]
    public void Purchase_ShouldHaveRequiredGuestName()
    {
        // Arrange
        var purchase = new Purchase
        {
            GuestEmail = "test@example.com",
            PurchaseDate = DateTime.UtcNow,
            TotalCost = 100.00m
        };
        var validationContext = new ValidationContext(purchase);

        // Act
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(purchase, validationContext, results, true);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains("GuestName"));
    }

    [Fact]
    public void Purchase_ShouldHaveValidEmail()
    {
        // Arrange
        var purchase = new Purchase
        {
            GuestName = "Test User",
            GuestEmail = "invalid-email", // Invalid email format
            PurchaseDate = DateTime.UtcNow,
            TotalCost = 100.00m
        };
        var validationContext = new ValidationContext(purchase);

        // Act
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(purchase, validationContext, results, true);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains("GuestEmail"));
    }

    [Fact]
    public void Purchase_WithValidData_ShouldPassValidation()
    {
        // Arrange
        var purchase = new Purchase
        {
            GuestName = "Test User",
            GuestEmail = "test@example.com",
            PurchaseDate = DateTime.UtcNow,
            TotalCost = 100.00m
        };
        var validationContext = new ValidationContext(purchase);

        // Act
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(purchase, validationContext, results, true);

        // Assert
        Assert.True(isValid);
    }
}


