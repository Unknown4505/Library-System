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
            var extractedApiKey = context.Request.Headers[APIKEYNAME].ToString();

            if (string.IsNullOrWhiteSpace(extractedApiKey))
            {
                context.Response.StatusCode = 401; // Unauthorized
                context.Response.ContentType = "application/json";
                var response = BookKiosk.Application.DTOs.Common.ApiResponseDto<object>.Error("UNAUTHORIZED", "API Key was not provided.");
                await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }));
                return;
            }

            var appSettingsApiKey = config.GetValue<string>("ApiSettings:ApiKey");

            if (string.IsNullOrWhiteSpace(appSettingsApiKey))
            {
                context.Response.StatusCode = 500; // Internal Server Error
                context.Response.ContentType = "application/json";
                var response = BookKiosk.Application.DTOs.Common.ApiResponseDto<object>.Error("INTERNAL_ERROR", "Server configuration error: API Key is missing.");
                await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }));
                return;
            }

            if (!string.Equals(appSettingsApiKey, extractedApiKey, StringComparison.Ordinal))
            {
                context.Response.StatusCode = 403; // Forbidden
                context.Response.ContentType = "application/json";
                var response = BookKiosk.Application.DTOs.Common.ApiResponseDto<object>.Error("FORBIDDEN", "Unauthorized client. Invalid API Key.");
                await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }));
                return;
            }
        }

        await _next(context);
    }
}
