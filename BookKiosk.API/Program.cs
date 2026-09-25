using Serilog;
using Microsoft.EntityFrameworkCore;
using BookKiosk.Infrastructure.Data;

// 1. Cấu hình Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("Logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Starting web application");
    
    var builder = WebApplication.CreateBuilder(args);

    // Sử dụng Serilog thay cho Logger mặc định
    builder.Host.UseSerilog();

    // 2. Add services to the container (Dependency Injection)
    builder.Services.AddControllers();
    
    // Đăng ký DbContext với SQL Server
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

    // Swagger/OpenAPI
    builder.Services.AddOpenApi();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    // 3. Tự động chạy Seed Data khi khởi động
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            DbInitializer.Initialize(services);
            Log.Information("Database initialization completed successfully.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while seeding the database.");
        }
    }

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    // Bật khả năng phục vụ file tĩnh (ảnh, tài liệu) từ thư mục wwwroot
    app.UseStaticFiles();

    app.UseHttpsRedirection();
    
    // Ghi log mọi request HTTP qua Serilog
    app.UseSerilogRequestLogging();

    app.UseAuthorization();
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
