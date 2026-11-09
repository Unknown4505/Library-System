extern alias BackendApi;
using System.Data;
using System.Data.Common;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using BookKiosk.Application.Services;
using BookKiosk.Domain.Entities;
using BookKiosk.Domain.Enums;
using BookKiosk.Infrastructure.Data;
using BookKiosk.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using BackendApi::BookKiosk.API.Controllers;

namespace BookKiosk.Tests.Backend;

public sealed class SqlAcceptanceTests(DisposableSqlQa db) : IClassFixture<DisposableSqlQa>
{
    [SqlQaFact]
    public async Task Barcode_cutover_duplicate_reservation_and_old_new_lookup()
    {
        var book = await db.AddBookAsync(); var other = await db.AddBookAsync();
        var next = Guid.NewGuid().ToString("N");
        await using var context = db.Context();
        var repository = new BookBarcodeRepository(context);
        Assert.Equal("BAD_REQUEST", (await repository.ChangeAsync(book.BookId, book.Barcode)).Code);
        Assert.Equal("NOT_FOUND", (await repository.ChangeAsync(int.MaxValue, next)).Code);
        Assert.Equal("CONFLICT", (await repository.ChangeAsync(book.BookId, other.Barcode)).Code);
        var result = await repository.ChangeAsync(book.BookId, next);
        Assert.True(result.Success); Assert.True(result.Data!.RequiresLabelReprint);
        var lookup = new BookRepository(context);
        Assert.Null(await lookup.GetByBarcodeAsync(book.Barcode));
        Assert.Equal(book.BookId, (await lookup.GetByBarcodeAsync(next))!.BookId);
        await context.Books.Where(x => x.BookId == book.BookId).ExecuteUpdateAsync(s => s.SetProperty(x => x.ReservedQuantity, 1));
        Assert.Equal("CONFLICT", (await repository.ChangeAsync(book.BookId, Guid.NewGuid().ToString("N"))).Code);
    }

    [SqlQaFact]
    public async Task Same_book_race_has_one_winner()
    {
        var book = await db.AddBookAsync();
        var gate = new BeforeBarcodeUpdate(async () => await Task.CompletedTask, 2);
        await using var a = db.Context(gate); await using var b = db.Context(gate);
        var results = await Task.WhenAll(new BookBarcodeRepository(a).ChangeAsync(book.BookId, Guid.NewGuid().ToString("N")),
            new BookBarcodeRepository(b).ChangeAsync(book.BookId, Guid.NewGuid().ToString("N")));
        Assert.Single(results, x => x.Success); Assert.Single(results, x => x.Code == "CONFLICT");
    }

    [SqlQaFact]
    public async Task Concurrent_unique_target_has_one_winner()
    {
        var first = await db.AddBookAsync(); var second = await db.AddBookAsync(); var target = Guid.NewGuid().ToString("N");
        var gate = new BeforeBarcodeUpdate(async () => await Task.CompletedTask, 2);
        await using var a = db.Context(gate); await using var b = db.Context(gate);
        var results = await Task.WhenAll(new BookBarcodeRepository(a).ChangeAsync(first.BookId, target), new BookBarcodeRepository(b).ChangeAsync(second.BookId, target));
        Assert.Single(results, x => x.Success); Assert.Single(results, x => x.Code == "CONFLICT");
        await using var verify = db.Context(); Assert.Equal(1, await verify.Books.CountAsync(x => x.Barcode == target));
    }

    [SqlQaFact]
    public async Task Reservation_race_prevents_barcode_write()
    {
        var book = await db.AddBookAsync();
        var interceptor = new BeforeBarcodeUpdate(async () =>
        {
            await using var reserve = db.Context();
            await reserve.Books.Where(x => x.BookId == book.BookId).ExecuteUpdateAsync(s => s.SetProperty(x => x.ReservedQuantity, 1));
        });
        await using var context = db.Context(interceptor);
        Assert.Equal("CONFLICT", (await new BookBarcodeRepository(context).ChangeAsync(book.BookId, "RACE" + book.BookId)).Code);
        await using var verify = db.Context();
        Assert.Equal(book.Barcode, (await verify.Books.SingleAsync(x => x.BookId == book.BookId)).Barcode);
    }

    [SqlQaFact]
    public async Task Persistence_failure_rolls_back_cutover()
    {
        var book = await db.AddBookAsync();
        await using var context = db.Context(new BeforeBarcodeUpdate(() => throw new DataException("injected failure")));
        await Assert.ThrowsAsync<DataException>(() => new BookBarcodeRepository(context).ChangeAsync(book.BookId, "FAIL" + book.BookId));
        await using var verify = db.Context();
        Assert.Equal(book.Barcode, (await verify.Books.SingleAsync(x => x.BookId == book.BookId)).Barcode);
    }

    [SqlQaFact]
    public async Task Real_jwt_http_authorization_and_cutover_contract()
    {
        var book = await db.AddBookAsync();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("QA_ONLY_EPHEMERAL_BARCODE_KEY_1234567890123456789"));
        using var host = await new HostBuilder().ConfigureWebHost(builder => builder.UseTestServer().ConfigureServices(services =>
        {
            services.AddRouting();
            services.AddControllers().AddApplicationPart(typeof(BooksBarcodeController).Assembly);
            services.AddScoped(_ => db.Context());
            services.AddScoped<IBookBarcodeRepository, BookBarcodeRepository>();
            services.AddScoped<IBookBarcodeService, BookBarcodeService>();
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => o.TokenValidationParameters = new()
            { ValidateIssuer = false, ValidateAudience = false, ValidateLifetime = true, ValidateIssuerSigningKey = true, IssuerSigningKey = key });
            services.AddAuthorization();
        }).Configure(app => { app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.UseEndpoints(e => e.MapControllers()); })).StartAsync();
        using var client = host.GetTestClient();
        var url = $"/api/books/{book.BookId}/change-barcode";
        async Task<HttpResponseMessage> Change(string barcode, string reason = "QA label replacement") =>
            await client.PostAsync(url, new StringContent(JsonSerializer.Serialize(new {newBarcode=barcode, reason}), Encoding.UTF8, "application/json"));
        using (var response = await Change("QA")) Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        foreach (var role in new[] {"Staff", "Manager", "Admin"})
        {
            var token = new JwtSecurityToken(claims: new[] { new Claim(ClaimTypes.NameIdentifier,"33"), new Claim(ClaimTypes.Role,role) },
                expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
            using var response = await Change("HTTP" + book.BookId);
            Assert.Equal(role == "Admin" ? HttpStatusCode.OK : HttpStatusCode.Forbidden, response.StatusCode);
        }
        using var invalid = await Change("", "bad"); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    private sealed class BeforeBarcodeUpdate(Func<Task> action, int participants = 1) : DbCommandInterceptor
    {
        private readonly TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrivals;
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("UPDATE") && command.CommandText.Contains("[Books]") && command.CommandText.Contains("[Barcode]"))
            {
                if (Interlocked.Increment(ref arrivals) == participants) gate.TrySetResult();
                await gate.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
                await action();
            }
            return result;
        }
    }
}
