using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace BookKiosk.API.Middleware;

public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private const string APIKEYNAME = "X-API-KEY";

    public ApiKeyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IConfiguration config)
    {
        // 1. Kiểm tra xem endpoint có cho phép gọi bằng API Key hay không (VD: Các API dành cho Kiosk)
        if (context.Request.Path.StartsWithSegments("/api/kiosk") || context.Request.Path.StartsWithSegments("/api/orders/kiosk"))
        {
            if (!context.Request.Headers.TryGetValue(APIKEYNAME, out var extractedApiKey))
            {
                context.Response.StatusCode = 401; // Unauthorized
                await context.Response.WriteAsync("API Key was not provided.");
                return;
            }

            var appSettingsApiKey = config.GetValue<string>("ApiKey");

            if (!appSettingsApiKey.Equals(extractedApiKey))
            {
                context.Response.StatusCode = 403; // Forbidden
                await context.Response.WriteAsync("Unauthorized client. Invalid API Key.");
                return;
            }
        }

        await _next(context);
    }
}
