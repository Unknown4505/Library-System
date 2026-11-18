using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BookKiosk.Application.Validation;
using Microsoft.EntityFrameworkCore;

namespace BookKiosk.Tests.Backend;

public sealed class SupplierValidationTests(DisposableSqlQa sql) : IClassFixture<DisposableSqlQa>
{
    [Theory]
    [InlineData(" 0912345678 ", "0912345678")]
    [InlineData("+84912345678", "0912345678")]
    [InlineData("02838221234", "02838221234")]
    [InlineData("+842838221234", "02838221234")]
    public void Supported_phone_is_normalized(string input, string expected) =>
        Assert.Equal(expected, SupplierContactPolicy.NormalizePhone(input));

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("   ")] [InlineData("212")]
    [InlineData("abcdefghij")] [InlineData("091234567")] [InlineData("09123456789")]
    [InlineData("0912 345 678")] [InlineData("+840912345678")] [InlineData("０９１２３４５６７８")]
    public void Invalid_phone_is_rejected(string? value) =>
        Assert.Throws<ArgumentException>(() => SupplierContactPolicy.NormalizePhone(value));

    [SqlQaFact]
    public async Task Http_create_update_validation_authorization_and_persistence()
    {
        await using var context = sql.Context();
        using var host = new BooksApiTestHost(context.Database.GetConnectionString());
        using var admin = host.Client("Admin");
        foreach (var role in new string?[] { null, "Invalid", "Staff" })
        {
            using var client = host.Client(role);
            using var denied = await client.PostAsJsonAsync("/api/suppliers", Payload("0912345678"));
            Assert.Equal(role == "Staff" ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized, denied.StatusCode);
        }
        foreach (var role in new[] { "Admin", "Manager" })
        {
            using var client = host.Client(role);
            using var created = await client.PostAsJsonAsync("/api/suppliers", Payload(" +84912345678 ", " qa@example.com "));
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
            using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
            var id = json.RootElement.GetProperty("data").GetProperty("supplierId").GetInt32();
            var saved = await context.Suppliers.AsNoTracking().SingleAsync(s => s.SupplierId == id);
            Assert.Equal("0912345678", saved.PhoneNumber);
            Assert.Equal("qa@example.com", saved.Email);
            Assert.Equal("QA contact", saved.ContactName);
            foreach (var payload in new object[] {
                Payload("212"), Payload("letters"), Payload(""), Payload("   "),
                Payload("09123456789"), Payload(new string('1',21)), Payload("0912345678", "invalid"),
                Payload("0912345678",new string('a',101)),
                new { name="QA supplier", email="a@example.com" },
                new { name=" ", phoneNumber="0912345678" },
                new { name="QA", phoneNumber="0912345678",contactName=new string('x',101) } })
            {
                var count = await context.Suppliers.CountAsync();
                using var post = await client.PostAsJsonAsync("/api/suppliers", payload);
                using var put = await client.PutAsJsonAsync($"/api/suppliers/{id}", payload);
                await Invalid(post); await Invalid(put);
                Assert.Equal(count, await context.Suppliers.CountAsync());
                var unchanged = await context.Suppliers.AsNoTracking().SingleAsync(s => s.SupplierId == id);
                Assert.Equal(saved.PhoneNumber, unchanged.PhoneNumber);
                Assert.Equal(saved.Email, unchanged.Email);
                Assert.Equal(saved.ContactName, unchanged.ContactName);
            }
            using var updated = await client.PutAsJsonAsync($"/api/suppliers/{id}", new { name="Updated", phone="+842838221234", email="   ",contactName=" Updated contact " });
            Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
            var after = await context.Suppliers.AsNoTracking().SingleAsync(s=>s.SupplierId==id);
            Assert.Equal("02838221234", after.PhoneNumber); Assert.Null(after.Email);
            Assert.Equal("Updated contact",after.ContactName);
            using var get = await client.GetAsync($"/api/suppliers/{id}");
            Assert.Equal(HttpStatusCode.OK,get.StatusCode);
            Assert.Contains("Updated contact",await get.Content.ReadAsStringAsync());
        }
        using var malformed = await admin.PostAsync("/api/suppliers", new StringContent("{",System.Text.Encoding.UTF8,"application/json"));
        await Invalid(malformed);
        using var missing = await admin.PutAsJsonAsync("/api/suppliers/2147483647",Payload("0912345678"));
        Assert.Equal(HttpStatusCode.NotFound,missing.StatusCode);
    }

    private static object Payload(string phone, string? email=null) => new { name="QA supplier", phoneNumber=phone, email, contactName=" QA contact " };
    private static async Task Invalid(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
        using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("BAD_REQUEST",json.RootElement.GetProperty("code").GetString());
    }
}
