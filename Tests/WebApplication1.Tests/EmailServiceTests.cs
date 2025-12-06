using Xunit;
using Moq;
using Microsoft.Extensions.Configuration;
using GBC_Ticketing.Web.Services;

namespace WebApplication1.Tests;

public class EmailServiceTests
{
    [Fact]
    public void EmailService_ShouldImplementIEmailService()
    {
        // Arrange
        var configMock = new Mock<IConfiguration>();
        var smtpSectionMock = new Mock<IConfigurationSection>();
        
        configMock.Setup(c => c["Smtp:From"]).Returns("test@example.com");
        configMock.Setup(c => c["Smtp:Host"]).Returns("smtp.test.com");
        configMock.Setup(c => c["Smtp:Port"]).Returns("587");
        configMock.Setup(c => c["Smtp:User"]).Returns("testuser");
        configMock.Setup(c => c["Smtp:Pass"]).Returns("testpass");

        // Act
        var service = new EmailService(configMock.Object);

        // Assert
        Assert.IsAssignableFrom<IEmailService>(service);
    }

    [Fact]
    public void EmailService_Constructor_ShouldNotThrow()
    {
        // Arrange
        var configMock = new Mock<IConfiguration>();
        
        // Act & Assert
        var exception = Record.Exception(() => new EmailService(configMock.Object));
        Assert.Null(exception);
    }
}

