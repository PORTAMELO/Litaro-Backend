using System.Security.Claims;

namespace Litaro.Services;

public class AuditContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AuditContext audit)
    {
        var request = context.Request;
        audit.SetRequest(
            $"{request.Method} {request.Path}",
            context.Connection.RemoteIpAddress?.ToString(),
            request.Headers.UserAgent.ToString());

        var user = context.User;
        if (user.Identity?.IsAuthenticated == true
            && int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            var name = $"{user.FindFirstValue(ClaimTypes.GivenName)} {user.FindFirstValue(ClaimTypes.Surname)}".Trim();
            audit.SetUser(userId, name);
        }
        else
        {
            audit.SetAnonymous();
        }

        await next(context);
    }
}
