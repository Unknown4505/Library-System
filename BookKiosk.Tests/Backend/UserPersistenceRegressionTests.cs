extern alias BackendApi;

using System.Reflection;
using BookKiosk.Application.DTOs.Users;
using BookKiosk.Application.Interfaces.Repositories;
using BookKiosk.Application.Services;
using BackendApi::BookKiosk.API.Middleware;
using BackendApi::BookKiosk.API.Controllers;
using BookKiosk.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BookKiosk.Tests.Backend;

public class UserPersistenceRegressionTests
{
    [Theory]
    [InlineData("2601")]
    [InlineData("2627")]
    [InlineData("IX_Users_Username")]
    [InlineData("duplicate key")]
    [InlineData("UNIQUE constraint failed")]
    public void Arbitrary_messages_and_lookalike_provider_types_are_not_unique(string message)
    {
        Assert.False(UserService.IsUniqueConstraintViolation(new Exception(message)));
        Assert.False(UserService.IsUniqueConstraintViolation(new DbUpdateException(message, new Exception(message))));
        Assert.False(UserService.IsUniqueConstraintViolation(new SqlException(message)));
    }

    [Theory]
    [InlineData(2601, true)]
    [InlineData(2627, true)]
    [InlineData(547, false)]
    [InlineData(515, false)]
    [InlineData(1205, false)]
    public void Actual_sql_provider_metadata_controls_classification(int number, bool expected)
    {
        var error = CreateSqlException(number);
        Assert.Equal(expected, UserService.IsUniqueConstraintViolation(error));
        Assert.Equal(expected, UserService.IsUniqueConstraintViolation(new DbUpdateException("wrapper", error)));
    }

    [Theory]
    [InlineData(2601)]
    [InlineData(2627)]
    public async Task Real_provider_unique_errors_keep_safe_409_contract(int number)
    {
        var repository = new Mock<IUserRepository>();
        var work = new Mock<IUnitOfWork>();
        work.Setup(x => x.SaveChangesAsync()).ThrowsAsync(new DbUpdateException("provider detail", CreateSqlException(number)));
        var controller = new UsersController(new UserService(repository.Object, work.Object));
        var result = Assert.IsType<ConflictObjectResult>(await controller.CreateUser(new CreateUserDto
        {
            Username = "regression_user", Password = "StrongPassword1", FullName = "Regression User", Role = UserRole.Admin
        }));
        var body = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains("CONFLICT", body);
        Assert.DoesNotContain("provider detail", body);
        Assert.DoesNotContain("duplicate key", body);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Non_unique_persistence_errors_reach_safe_500(bool invalidOperation)
    {
        const string detail = "provider secret: 2627 duplicate key IX_Users_Username";
        Exception failure = invalidOperation ? new InvalidOperationException(detail) : new Exception(detail);
        var repository = new Mock<IUserRepository>();
        var work = new Mock<IUnitOfWork>();
        work.Setup(x => x.SaveChangesAsync()).ThrowsAsync(failure);
        var service = new UserService(repository.Object, work.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new GlobalExceptionHandlerMiddleware(async _ =>
            await service.CreateUserAsync(new CreateUserDto
            {
                Username = "regression_user", Password = "StrongPassword1", FullName = "Regression User", Role = UserRole.Admin
            }), NullLogger<GlobalExceptionHandlerMiddleware>.Instance);

        await middleware.InvokeAsync(context);
        Assert.Equal(500, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Contains("INTERNAL_ERROR", body);
        Assert.DoesNotContain("provider secret", body);
        Assert.DoesNotContain("2627", body);
    }

    private static Microsoft.Data.SqlClient.SqlException CreateSqlException(int number)
    {
        // Construct the real sealed provider exception without requiring a live SQL server.
        var errorConstructor = typeof(SqlError).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .First(c => c.GetParameters().Length == 9 && c.GetParameters()[7].ParameterType == typeof(Exception));
        var error = errorConstructor.Invoke(new object?[] { number, (byte)1, (byte)14, "server", "duplicate key 2601 2627", "", 1, null, -1 });
        var errors = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), true)!;
        typeof(SqlErrorCollection).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(errors, new[] { error });
        var factory = typeof(Microsoft.Data.SqlClient.SqlException).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .First(m => m.Name == "CreateException" && m.GetParameters().Length == 2);
        return (Microsoft.Data.SqlClient.SqlException)factory.Invoke(null, new object[] { errors, "16.0" })!;
    }

    private sealed class SqlException(string message) : Exception(message)
    {
        public int Number => 2627;
    }
}
