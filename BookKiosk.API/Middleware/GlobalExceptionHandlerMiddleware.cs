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
            ArgumentException or ArgumentNullException => (int)HttpStatusCode.BadRequest, // 400
            Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException => (int)HttpStatusCode.Conflict, // 409 - Lỗi ghi đè dữ liệu (Booking trùng sách)
            _ => (int)HttpStatusCode.InternalServerError // 500 - Lỗi mặc định chưa lường trước
        };

        var result = JsonSerializer.Serialize(new
        {
            StatusCode = context.Response.StatusCode,
            Message = context.Response.StatusCode == 500 
                ? "Hệ thống đang gặp sự cố nội bộ. Vui lòng thử lại sau!" 
                : exception.Message,
            ErrorDetail = exception.Message // Lưu ý: Nên ẩn Detail trên Production thực tế đối với lỗi 500
        });

        return context.Response.WriteAsync(result);
    }
}
