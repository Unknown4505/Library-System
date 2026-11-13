extern alias BackendApi;
using System.Text.Json;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using BackendApi::BookKiosk.API.Middleware;

namespace BookKiosk.Tests.Backend;

public class BooksDiagnosticRouteTests
{
    [Fact]
    public async Task Production_pipeline_has_no_throwing_route_in_any_build()
    {
        using var host = new BooksApiTestHost();
        using var client = host.Client();
        var response = await client.GetAsync("/api/books/test-error");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(typeof(BackendApi::BookKiosk.API.Controllers.BooksController).GetMethod("TestError"));
        Assert.Equal(System.Net.HttpStatusCode.OK, (await client.GetAsync("/api/books")).StatusCode);
    }

    [Fact]
    public async Task Production_pipeline_keeps_admin_book_crud_available()
    {
        using var host=new BooksApiTestHost();
        using var client=host.Client("Admin");
        using var list=JsonDocument.Parse(await client.GetStringAsync("/api/books"));
        var category=list.RootElement.GetProperty("data").GetProperty("items")[0].GetProperty("categoryId").GetInt32();
        var payload=new { barcode=Guid.NewGuid().ToString("N"),title="QA route removal",author="QA",categoryId=category,
            costPrice=0,sellingPrice=10000,stockQuantity=1,isActive=true };
        var response=await client.PostAsJsonAsync("/api/books",payload);
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        using var created=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var id=created.RootElement.GetProperty("data").GetInt32();
        Assert.Equal(HttpStatusCode.OK,(await client.PutAsJsonAsync($"/api/books/{id}",payload)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.DeleteAsync($"/api/books/{id}")).StatusCode);
    }

    [Fact]
    public async Task Unexpected_error_is_logged_but_response_contains_only_safe_wrapper()
    {
        var error = new DivideByZeroException("private-qa-provider-detail");
        var logger = new Mock<ILogger<GlobalExceptionHandlerMiddleware>>();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new GlobalExceptionHandlerMiddleware(_ => throw error, logger.Object);
        await middleware.InvokeAsync(context);
        Assert.Equal(500, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var text = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.DoesNotContain(error.Message, text);
        using var json = JsonDocument.Parse(text);
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("INTERNAL_ERROR", json.RootElement.GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("data").ValueKind);
        logger.Verify(x => x.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), error,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }
}
