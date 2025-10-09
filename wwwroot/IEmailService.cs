using System.Threading.Tasks;

namespace GBC_Ticketing.Web.Services;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body);
}