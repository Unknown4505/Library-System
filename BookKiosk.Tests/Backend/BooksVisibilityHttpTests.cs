using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace BookKiosk.Tests.Backend;

public class BooksVisibilityHttpTests : IClassFixture<DisposableSqlQa>
{
    private readonly DisposableSqlQa sql;
    public BooksVisibilityHttpTests(DisposableSqlQa sql) => this.sql = sql;

    [SqlQaFact]
    public async Task Inactive_id_barcode_paging_and_reactivation_matrix_uses_real_jwt_and_sql()
    {
        var active = await sql.AddBookAsync();
        var inactive = await sql.AddBookAsync();
        await using (var db = sql.Context())
        {
            var b = await db.Books.FindAsync(inactive.BookId);
            b!.IsActive = false; await db.SaveChangesAsync();
        }
        await using var context = sql.Context();
        using var host = new BooksApiTestHost(context.Database.GetConnectionString());
        foreach (var role in new string?[] { null, "Invalid", "Staff", "Manager", "Admin" })
        {
            using var client = host.Client(role);
            foreach (var book in new[] { active, inactive })
            {
                var id = await client.GetAsync($"/api/books/{book.BookId}");
                Assert.Equal(book == active || role == "Admin" ? HttpStatusCode.OK : HttpStatusCode.NotFound, id.StatusCode);
                var barcode = await client.GetAsync($"/api/books/barcode/{book.Barcode}");
                Assert.Equal(book == active ? HttpStatusCode.OK : HttpStatusCode.NotFound, barcode.StatusCode);
                if (id.StatusCode == HttpStatusCode.NotFound) await AssertNotFound(id);
                if (barcode.StatusCode == HttpStatusCode.NotFound) await AssertNotFound(barcode);
            }
            await AssertNotFound(await client.GetAsync("/api/books/2147483647"));
            await AssertNotFound(await client.GetAsync("/api/books/barcode/not-present"));
            using var page = JsonDocument.Parse(await client.GetStringAsync("/api/books?page=1&pageSize=100&includeInactive=true"));
            var items = page.RootElement.GetProperty("data").GetProperty("items");
            Assert.Contains(items.EnumerateArray(), b => b.GetProperty("bookId").GetInt32() == active.BookId);
            Assert.DoesNotContain(items.EnumerateArray(), b => b.GetProperty("bookId").GetInt32() == inactive.BookId);
            Assert.All(items.EnumerateArray(), b => Assert.True(b.GetProperty("isActive").GetBoolean()));
        }
        // Repository must still see the inactive record for Admin CRUD.
        Assert.False((await context.Books.AsNoTracking().SingleAsync(b => b.BookId == inactive.BookId)).IsActive);
        using var admin = host.Client("Admin");
        var payload = new { title="QA edited", author="QA", sellingPrice=10000, costPrice=0, stockQuantity=10,
            categoryId=inactive.CategoryId, isActive=false };
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/books/{inactive.BookId}", payload)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/books/{inactive.BookId}", new {
            payload.title, payload.author, payload.sellingPrice, payload.costPrice, payload.stockQuantity, payload.categoryId, isActive=true })).StatusCode);
        using var anonymous = host.Client();
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"/api/books/{inactive.BookId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"/api/books/barcode/{inactive.Barcode}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/books/{inactive.BookId}")).StatusCode);
        await AssertNotFound(await anonymous.GetAsync($"/api/books/{inactive.BookId}"));
        await AssertNotFound(await admin.GetAsync($"/api/books/barcode/{inactive.Barcode}"));
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/books/{inactive.BookId}")).StatusCode);
        var saved = await context.Books.AsNoTracking().SingleAsync(b => b.BookId == inactive.BookId);
        Assert.Equal(inactive.Barcode, saved.Barcode);
        Assert.Equal("QA edited", saved.Title);
        Assert.False(saved.IsActive);
    }
    private static async Task AssertNotFound(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("NOT_FOUND", json.RootElement.GetProperty("code").GetString());
    }
}
