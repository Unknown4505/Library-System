extern alias BackendApi;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using BookKiosk.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace BookKiosk.Tests.Backend;

// Runs the real API pipeline/JWT validator. Startup seeds only an isolated InMemory DB;
// SQL requests use exclusively the generated disposable fixture DB, without migrations.
public sealed class BooksApiTestHost : WebApplicationFactory<BackendApi::BookKiosk.API.Controllers.BooksController>
{
    private readonly string key = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
    private readonly string database = Guid.NewGuid().ToString("N");
    private readonly string root = Path.Combine(Path.GetTempPath(), "BookKiosk_BooksQA_" + Guid.NewGuid().ToString("N"));
    private readonly string? sql;
    private bool ready;
    public string UploadRoot => Path.Combine(root, "uploads", "books");
    public BooksApiTestHost(string? sql = null) { this.sql = sql; Directory.CreateDirectory(root); }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(c => c.AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["Jwt:Key"] = key, ["ApiSettings:ApiKey"] = key,
            ["Jwt:Issuer"] = "BooksQA", ["Jwt:Audience"] = "BooksQA"
        }));
        return base.CreateHost(builder);
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseWebRoot(root);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(o =>
            {
                if (ready && sql != null) o.UseSqlServer(sql);
                else o.UseInMemoryDatabase(database);
            });
            foreach (var d in services.Where(d => d.ServiceType == typeof(IHostedService) &&
                d.ImplementationType == typeof(BackendApi::BookKiosk.API.HostedServices.ExpiredOrderCleanupService)).ToArray()) services.Remove(d);
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o => o.TokenValidationParameters = new()
            {
                ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
                ValidIssuer = "BooksQA", ValidAudience = "BooksQA", IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
            });
        });
    }
    public HttpClient Client(string? role = null)
    {
        var client = CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        ready = true;
        if (role == "Invalid") client.DefaultRequestHeaders.Authorization = new("Bearer", "invalid.jwt.signature");
        else if (role != null)
        {
            var token = new JwtSecurityToken("BooksQA", "BooksQA", new[] { new Claim(ClaimTypes.NameIdentifier,"33"), new Claim(ClaimTypes.Role,role) },
                expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        }
        return client;
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(root))
        {
            var parent = Path.GetFullPath(Path.GetTempPath());
            var target = Path.GetFullPath(root);
            if (!target.StartsWith(parent, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(target).StartsWith("BookKiosk_BooksQA_"))
                throw new InvalidOperationException("Unsafe QA cleanup target.");
            Directory.Delete(target, true);
            Assert.False(Directory.Exists(target));
        }
    }
}
