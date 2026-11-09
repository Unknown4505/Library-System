using BookKiosk.Domain.Entities;
using BookKiosk.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BookKiosk.Tests.Backend;

// Explicit opt-in. The connection must have permission to create/drop ONLY a fresh QA database.
public sealed class SqlQaFactAttribute : FactAttribute
{
    public SqlQaFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BOOKKIOSK_QA_SQL_MASTER")))
            Skip = "SQL acceptance not run: set BOOKKIOSK_QA_SQL_MASTER to an approved disposable QA SQL Server.";
    }
}

public sealed class DisposableSqlQa : IAsyncLifetime
{
    private readonly string name = "BookKiosk_QA_" + Guid.NewGuid().ToString("N");
    private string? connection;
    private string? master;
    private bool created;

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("BOOKKIOSK_QA_SQL_MASTER");
        if (string.IsNullOrWhiteSpace(configured)) return;
        var builder = new SqlConnectionStringBuilder(configured) { InitialCatalog = "master" };
        master = builder.ConnectionString;
        await using var server = new SqlConnection(master);
        await server.OpenAsync();
        await using var create = new SqlCommand($"CREATE DATABASE [{name}]", server);
        await create.ExecuteNonQueryAsync();
        created = true;
        builder.InitialCatalog = name;
        connection = builder.ConnectionString;
        try
        {
            await using var context = Context();
            // Fresh generated QA DB only. Never run production startup/seeding or migrations here.
            await context.Database.EnsureCreatedAsync();
        }
        catch { await DisposeAsync(); throw; }
    }

    public ApplicationDbContext Context(DbCommandInterceptor? interceptor = null)
    {
        if (connection == null) throw new InvalidOperationException("SQL QA is not configured.");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection);
        if (interceptor != null) options.AddInterceptors(interceptor);
        return new ApplicationDbContext(options.Options);
    }

    public async Task<Book> AddBookAsync()
    {
        await using var context = Context();
        var book = new Book { Barcode = Guid.NewGuid().ToString("N"), Title = "QA book", Author = "QA", StockQuantity = 10,
            SellingPrice = 10000, Category = new Category { Name = "QA " + Guid.NewGuid().ToString("N") } };
        context.Books.Add(book); await context.SaveChangesAsync(); return book;
    }

    public async Task DisposeAsync()
    {
        if (!created) return;
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^BookKiosk_QA_[a-f0-9]{32}$")) throw new InvalidOperationException("Unsafe cleanup target.");
        SqlConnection.ClearAllPools();
        await using var server = new SqlConnection(master);
        await server.OpenAsync();
        await using var drop = new SqlCommand($"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]", server);
        await drop.ExecuteNonQueryAsync();
        await using var check = new SqlCommand("SELECT COUNT(*) FROM sys.databases WHERE name=@name", server);
        check.Parameters.AddWithValue("@name", name);
        Assert.Equal(0, Convert.ToInt32(await check.ExecuteScalarAsync()));
        created = false;
    }
}

