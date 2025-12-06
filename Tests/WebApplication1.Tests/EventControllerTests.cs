using Xunit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GBC_Ticketing.Web.Controllers;
using GBC_Ticketing.Web.Models;

namespace WebApplication1.Tests;

public class EventControllerTests
{
    private ApplicationDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task Index_ReturnsViewResult_WithEvents()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var category = new Category { Name = "Test Category" };
        context.Categories.Add(category);
        context.Events.Add(new Event
        {
            Title = "Test Event",
            CategoryId = category.Id,
            EventDate = DateTime.UtcNow.AddDays(30),
            TicketPrice = 50.00m,
            AvailableTickets = 100
        });
        await context.SaveChangesAsync();

        var controller = new EventController(context);

        // Act
        var result = await controller.Index(null, null, null, null, null, null, null);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IEnumerable<Event>>(viewResult.Model);
        Assert.Single(model);
    }

    [Fact]
    public async Task Details_WithValidId_ReturnsViewResult()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var category = new Category { Name = "Test Category" };
        context.Categories.Add(category);
        var @event = new Event
        {
            Title = "Test Event",
            CategoryId = category.Id,
            EventDate = DateTime.UtcNow.AddDays(30),
            TicketPrice = 50.00m,
            AvailableTickets = 100
        };
        context.Events.Add(@event);
        await context.SaveChangesAsync();

        var controller = new EventController(context);

        // Act
        var result = await controller.Details(@event.Id);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<Event>(viewResult.Model);
        Assert.Equal(@event.Id, model.Id);
    }

    [Fact]
    public async Task Details_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var controller = new EventController(context);

        // Act
        var result = await controller.Details(999);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Create_WithValidModel_RedirectsToIndex()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var category = new Category { Name = "Test Category" };
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var controller = new EventController(context);
        var @event = new Event
        {
            Title = "New Event",
            CategoryId = category.Id,
            EventDate = DateTime.UtcNow.AddDays(30),
            TicketPrice = 50.00m,
            AvailableTickets = 100
        };

        // Act
        var result = await controller.Create(@event);

        // Assert
        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirectResult.ActionName);
    }
}

