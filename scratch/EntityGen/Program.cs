using System;
using System.Linq;
using BookKiosk.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddDbContext<ApplicationDbContext>(options =>
    options.UseInMemoryDatabase("TestSeedDB"));

var serviceProvider = services.BuildServiceProvider();

using (var scope = serviceProvider.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    
    Console.WriteLine("Running DbInitializer.Initialize...");
    DbInitializer.Initialize(scope.ServiceProvider);
    
    Console.WriteLine($"Users Count: {context.Users.Count()}");
    Console.WriteLine($"Kiosks Count: {context.Kiosks.Count()}");
    Console.WriteLine($"Promotions Count: {context.Promotions.Count()}");
    Console.WriteLine($"PromotionOrderDiscounts Count: {context.PromotionOrderDiscounts.Count()}");
    Console.WriteLine("DbInitializer executed successfully!");
}
