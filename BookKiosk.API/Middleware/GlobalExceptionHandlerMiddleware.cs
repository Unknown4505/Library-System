using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BookKiosk.API.Middleware;

public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi Server (Uncaught Exception): {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        // Tùy theo loại Exception mà trả về Status Code khác nhau cho tường minh
        context.Response.StatusCode = exception switch
        {
            KeyNotFoundException => (int)HttpStatusCode.NotFound, // 404
            UnauthorizedAccessException => (int)HttpStatusCode.Forbidden, // 403
            ArgumentException => (int)HttpStatusCode.BadRequest, // 400
            Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException => (int)HttpStatusCode.Conflict, // 409
            _ => (int)HttpStatusCode.InternalServerError // 500
        };

        var errorCode = context.Response.StatusCode switch
        {
            400 => "BAD_REQUEST",
            404 => "NOT_FOUND",
            403 => "FORBIDDEN",
            409 => "CONFLICT",
            _ => "INTERNAL_ERROR"
        };

        var message = context.Response.StatusCode == 500
            ? "Hệ thống đang gặp sự cố nội bộ. Vui lòng thử lại sau!"
            : exception.Message;

        var response = BookKiosk.Application.DTOs.Common.ApiResponseDto<object>.Error(errorCode, message);

        var result = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return context.Response.WriteAsync(result);
    }
}
