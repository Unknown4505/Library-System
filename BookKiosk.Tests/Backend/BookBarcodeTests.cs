extern alias BackendApi;
using System.Security.Claims;
using BookKiosk.Application.DTOs.Books;
using BookKiosk.Application.DTOs.Common;
using BookKiosk.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using BackendApi::BookKiosk.API.Controllers;

namespace BookKiosk.Tests.Backend;

public class BookBarcodeTests
{
    [Theory]
    [InlineData("", "valid reason")]
    [InlineData("abc\nxyz", "valid reason")]
    [InlineData("new", "bad")]
    [InlineData("new", "")]
    public async Task Invalid_request_never_calls_persistence(string barcode, string reason)
    {
        var repo = new Mock<IBookBarcodeRepository>(MockBehavior.Strict);
        var service = new BookBarcodeService(repo.Object, Mock.Of<ILogger<BookBarcodeService>>());
        var result = await service.ChangeAsync(1, new() { NewBarcode = barcode, Reason = reason }, 1, "test");
        Assert.Equal("BAD_REQUEST", result.Code);
        repo.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(51, 10)]
    [InlineData(5, 501)]
    public async Task Length_limits_are_checked_after_trim(int barcodeLength, int reasonLength)
    {
        var service = new BookBarcodeService(Mock.Of<IBookBarcodeRepository>(), Mock.Of<ILogger<BookBarcodeService>>());
        Assert.Equal("BAD_REQUEST", (await service.ChangeAsync(1, new() { NewBarcode = new('a', barcodeLength), Reason = new('b', reasonLength) }, 1, "test")).Code);
    }

    [Fact]
    public async Task Successful_cutover_trims_request_and_logs_committed_audit()
    {
        var repo = new Mock<IBookBarcodeRepository>();
        repo.Setup(x => x.ChangeAsync(1, "NEW")).ReturnsAsync(ApiResponseDto<ChangeBarcodeDto>.Ok(new(1, "OLD", "NEW", true, DateTime.UtcNow)));
        var logger = new Mock<ILogger<BookBarcodeService>>();
        var result = await new BookBarcodeService(repo.Object, logger.Object).ChangeAsync(1, new() { NewBarcode = " NEW ", Reason = " New label required " }, 33, "correlation");
        Assert.True(result.Success);
        Assert.True(result.Data!.RequiresLabelReprint);
        Assert.Single(logger.Invocations, x => x.Method.Name == "Log");
    }

    [Fact]
    public async Task Committed_change_returns_success_with_operator_warning_when_audit_logger_throws()
    {
        var repo = new Mock<IBookBarcodeRepository>();
        repo.Setup(x => x.ChangeAsync(1, "NEW")).ReturnsAsync(ApiResponseDto<ChangeBarcodeDto>.Ok(new(1, "OLD", "NEW", true, DateTime.UtcNow)));
        var result = await new BookBarcodeService(repo.Object, new ThrowingBarcodeLogger()).ChangeAsync(1,
            new() { NewBarcode = "NEW", Reason = "Print new label" }, 33, "audit-failure-test");
        Assert.True(result.Success);
        Assert.Equal("SUCCESS", result.Code);
        Assert.Equal("NEW", result.Data!.NewBarcode);
        Assert.Contains("nhật ký", result.Message);
        Assert.DoesNotContain("injected logger failure", result.Message);
    }

    [Fact]
    public async Task Failed_transaction_never_attempts_success_audit()
    {
        var repo = new Mock<IBookBarcodeRepository>();
        repo.Setup(x => x.ChangeAsync(1, "NEW")).ThrowsAsync(new System.Data.DataException("rollback"));
        var logger = new Mock<ILogger<BookBarcodeService>>(MockBehavior.Strict);
        await Assert.ThrowsAsync<System.Data.DataException>(() => new BookBarcodeService(repo.Object, logger.Object)
            .ChangeAsync(1, new() { NewBarcode = "NEW", Reason = "Print new label" }, 33, "test"));
        logger.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("NOT_FOUND", 404)]
    [InlineData("CONFLICT", 409)]
    [InlineData("BAD_REQUEST", 400)]
    public async Task Controller_preserves_wrapper_status(string code, int status)
    {
        var service = new Mock<IBookBarcodeService>();
        service.Setup(x => x.ChangeAsync(1, It.IsAny<ChangeBarcodeRequestDto>(), 33, It.IsAny<string>()))
            .ReturnsAsync(ApiResponseDto<ChangeBarcodeDto>.Error(code, "safe"));
        var controller = new BooksBarcodeController(service.Object) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "33") }, "test"));
        var result = Assert.IsAssignableFrom<ObjectResult>(await controller.ChangeBarcode(1, new()));
        Assert.Equal(status, result.StatusCode);
    }

    [Fact]
    public void Endpoint_has_admin_only_server_authorization_and_general_put_has_no_barcode()
    {
        var auth = Assert.Single(typeof(BooksBarcodeController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal("Admin", auth.Roles);
        Assert.Null(typeof(UpdateBookDto).GetProperty("Barcode"));
    }

    [Fact]
    public async Task Conflict_does_not_emit_success_audit()
    {
        var repo = new Mock<IBookBarcodeRepository>();
        repo.Setup(x => x.ChangeAsync(1, "NEW")).ReturnsAsync(ApiResponseDto<ChangeBarcodeDto>.Error("CONFLICT", "safe"));
        var logger = new Mock<ILogger<BookBarcodeService>>();
        var result = await new BookBarcodeService(repo.Object, logger.Object).ChangeAsync(1, new() { NewBarcode = "NEW", Reason = "Print new label" }, 33, "correlation");
        Assert.False(result.Success);
        Assert.Empty(logger.Invocations);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Invalid_actor_never_calls_persistence(int actor)
    {
        var repo = new Mock<IBookBarcodeRepository>(MockBehavior.Strict);
        var result = await new BookBarcodeService(repo.Object, Mock.Of<ILogger<BookBarcodeService>>()).ChangeAsync(1,
            new() { NewBarcode = "NEW", Reason = "Print new label" }, actor, "test");
        Assert.Equal("UNAUTHORIZED", result.Code);
        repo.VerifyNoOtherCalls();
    }
}

internal sealed class ThrowingBarcodeLogger : ILogger<BookBarcodeService>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter) => throw new InvalidOperationException("injected logger failure");
}
