using BookKiosk.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BookKiosk.API;

/// <summary>
/// Creates the EF model for design-time commands without starting the API or running DbInitializer.
/// </summary>
public sealed class DesignTimeApplicationDbContextFactory
    : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(
                "Server=bookkiosk-design-time.invalid,1433;" +
                "Database=BookKiosk_DesignTime_Only;User Id=invalid;Password=invalid;" +
                "Encrypt=True;TrustServerCertificate=False;Connect Timeout=1")
            .Options;

        return new ApplicationDbContext(options);
    }
}
