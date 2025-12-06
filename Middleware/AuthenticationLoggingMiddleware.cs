using Serilog;

namespace GBC_Ticketing.Web.Middleware;

public class AuthenticationLoggingMiddleware
{
    private readonly RequestDelegate _next;

    public AuthenticationLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Log failed authentication attempts (401/403)
        if (context.Response.StatusCode == 401 || context.Response.StatusCode == 403)
        {
            var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var path = context.Request.Path;
            var email = context.User?.Identity?.Name ?? "Unknown";
            
            Log.Warning("Failed authentication attempt - User: {Email}, Path: {Path}, IP: {IP}, Status: {Status}", 
                email, path, ipAddress, context.Response.StatusCode);
        }

        await _next(context);
    }
}

