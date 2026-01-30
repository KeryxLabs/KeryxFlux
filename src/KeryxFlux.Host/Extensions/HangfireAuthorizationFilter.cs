using Hangfire.Dashboard;

namespace KeryxFlux.Host.Extensions;

/// <summary>
/// Authorization filter for Hangfire Dashboard.
/// WARNING: This allows anonymous access - MUST be secured in production!
/// </summary>
public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        // TODO: Implement proper authorization in production
        // Options:
        // 1. Check for authentication cookie
        // 2. Check for API key header
        // 3. Check IP whitelist
        // 4. Integration with your auth system
        
        // For now, allow localhost only
        var httpContext = context.GetHttpContext();
        var remoteAddress = httpContext.Connection.RemoteIpAddress;
        
        // Allow localhost
        if (remoteAddress != null && 
            (remoteAddress.ToString() == "::1" || remoteAddress.ToString() == "127.0.0.1"))
        {
            return true;
        }
        
        // TODO: Add your authorization logic here
        return false;
    }
}
