using Microsoft.AspNetCore.RateLimiting;
using Serilog;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
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
    // Admit at most two cover uploads before MVC buffers multipart bodies. No queue.
    builder.Services.AddRateLimiter(options =>
    {
        options.AddConcurrencyLimiter("book-cover-upload", limiter =>
        {
            limiter.PermitLimit = BookKiosk.API.Services.BookCoverImageValidator.MaxConcurrentUploads;
            limiter.QueueLimit = 0;
        });
        options.OnRejected = async (context, cancellationToken) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.HttpContext.Response.Headers.RetryAfter = "1";
            await context.HttpContext.Response.WriteAsJsonAsync(
                BookKiosk.Application.DTOs.Common.ApiResponseDto<object>.Error("TOO_MANY_REQUESTS", "Hệ thống đang xử lý ảnh bìa. Vui lòng thử lại."),
                cancellationToken: cancellationToken);
        };
    });
    builder.Services.AddScoped<BookKiosk.Application.Services.IBookBarcodeService, BookKiosk.Application.Services.BookBarcodeService>();
    builder.Services.AddScoped<BookKiosk.Application.Services.IBookBarcodeRepository, BookKiosk.Infrastructure.Repositories.BookBarcodeRepository>();
    builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
    {
        var fallback = options.InvalidModelStateResponseFactory;
        options.InvalidModelStateResponseFactory = context =>
            context.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor action &&
            action.ControllerTypeInfo.AsType() == typeof(BookKiosk.API.Controllers.BooksBarcodeController)
                ? new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(
                    BookKiosk.Application.DTOs.Common.ApiResponseDto<BookKiosk.Application.DTOs.Books.ChangeBarcodeDto>
                        .Error("BAD_REQUEST", "Dữ liệu yêu cầu không hợp lệ."))
                : fallback(context);
    });


    // Users owns its validation wrapper; preserve existing contracts for other controllers.
    builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
    {
        var fallback = options.InvalidModelStateResponseFactory;
        options.InvalidModelStateResponseFactory = context =>
        {
            if (context.ActionDescriptor is not Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor action ||
                action.ControllerTypeInfo.AsType() != typeof(BookKiosk.API.Controllers.UsersController))
                return fallback(context);

            var errorMessage = context.ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Dữ liệu đầu vào không hợp lệ." : e.ErrorMessage)
                .FirstOrDefault() ?? "Dữ liệu đầu vào không hợp lệ.";

            var response = BookKiosk.Application.DTOs.Common.ApiResponseDto<object>.Error("BAD_REQUEST", errorMessage);
            return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(response);
        };
    });
    
    // Đăng ký DbContext với SQL Server
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

    // Đăng ký Repository và Service (Module 9)
    builder.Services.AddScoped<BookKiosk.Application.Interfaces.Repositories.IBookRepository, BookKiosk.Infrastructure.Repositories.BookRepository>();
    builder.Services.AddScoped<BookKiosk.Application.Interfaces.Services.IBookService, BookKiosk.Application.Services.BookService>();
    
    // Đăng ký Services mới thêm (Thien33)
    builder.Services.AddScoped<BookKiosk.Application.Services.IOrderService, BookKiosk.Application.Services.OrderService>();
    builder.Services.AddScoped<BookKiosk.Application.Services.IPaymentService, BookKiosk.Application.Services.PaymentService>();
    builder.Services.AddScoped<BookKiosk.Application.Services.IMemberService, BookKiosk.Application.Services.MemberService>();
    builder.Services.AddScoped<BookKiosk.Application.Services.IPromotionService, BookKiosk.Application.Services.PromotionService>();
    builder.Services.AddScoped<BookKiosk.Application.Services.IReportService, BookKiosk.Application.Services.ReportService>();
    builder.Services.AddScoped<BookKiosk.Application.Services.IInventoryService, BookKiosk.Application.Services.InventoryService>();
    builder.Services.AddScoped<BookKiosk.Application.Interfaces.Services.IKioskService, BookKiosk.Application.Services.KioskService>();
    builder.Services.AddScoped<BookKiosk.Application.Interfaces.Services.IKioskAdvertisingService, BookKiosk.Application.Services.KioskAdvertisingService>();
    builder.Services.AddScoped<BookKiosk.Application.Services.IAuthService, BookKiosk.Application.Services.AuthService>();
    builder.Services.AddScoped<BookKiosk.Application.Services.ISupplierService, BookKiosk.Application.Services.SupplierService>();
    builder.Services.AddScoped<BookKiosk.Application.Services.IUserService, BookKiosk.Application.Services.UserService>();

    // Đăng ký Unit Of Work & Repositories (Thien33)
    builder.Services.AddScoped<BookKiosk.Application.Interfaces.Repositories.IUnitOfWork, BookKiosk.Infrastructure.Repositories.UnitOfWork>();
    builder.Services.AddScoped<BookKiosk.Application.Interfaces.Repositories.IUserRepository, BookKiosk.Infrastructure.Repositories.UserRepository>();
    builder.Services.AddScoped<BookKiosk.Application.Interfaces.Repositories.ISupplierRepository, BookKiosk.Infrastructure.Repositories.SupplierRepository>();
    builder.Services.AddScoped<BookKiosk.Application.Interfaces.Repositories.IMemberRepository, BookKiosk.Infrastructure.Repositories.MemberRepository>();
    builder.Services.AddScoped<BookKiosk.Application.Interfaces.Repositories.IPromotionRepository, BookKiosk.Infrastructure.Repositories.PromotionRepository>();
    builder.Services.AddScoped<BookKiosk.Application.Interfaces.Repositories.IReportRepository, BookKiosk.Infrastructure.Repositories.ReportRepository>();
    builder.Services.AddScoped<BookKiosk.Application.Interfaces.Repositories.IInventoryRepository, BookKiosk.Infrastructure.Repositories.InventoryRepository>();
    builder.Services.AddScoped<BookKiosk.Application.Interfaces.Repositories.IOrderRepository, BookKiosk.Infrastructure.Repositories.OrderRepository>();
    builder.Services.AddScoped<BookKiosk.Application.Interfaces.Repositories.IPaymentRepository, BookKiosk.Infrastructure.Repositories.PaymentRepository>();
    builder.Services.AddScoped<BookKiosk.Application.Interfaces.Repositories.IKioskRepository, BookKiosk.Infrastructure.Repositories.KioskRepository>();
    builder.Services.AddScoped<BookKiosk.Application.Interfaces.Repositories.IKioskAdvertisingRepository, BookKiosk.Infrastructure.Repositories.KioskAdvertisingRepository>();
    
    // Đăng ký AutoMapper
    builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

    builder.Services.AddHostedService<BookKiosk.API.HostedServices.ExpiredOrderCleanupService>();

    // Swagger/OpenAPI
    builder.Services.AddOpenApi();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = "X-API-KEY",
            Description = "API key dành cho các endpoint Kiosk."
        });
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "JWT Bearer token dành cho CMS."
        });
    });

    // Cấu hình CORS cho phép CMS Web gọi API
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?? Array.Empty<string>();

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
    });

    // Cấu hình Authentication (JWT Bearer)
    var jwtKey = builder.Configuration["Jwt:Key"];
    if (string.IsNullOrWhiteSpace(jwtKey))
    {
        if (builder.Environment.IsProduction())
        {
            throw new InvalidOperationException(
                "Missing required configuration: Jwt:Key. Set it with environment variables or a secret store.");
        }

        jwtKey = "Day_La_Mot_Khoa_Bi_Mat_Dai_Nhat_Co_The_123456789";
        Log.Warning("Jwt:Key is not configured; using the Development-only demo key.");
    }

    var apiKey = builder.Configuration["ApiSettings:ApiKey"];
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        throw new InvalidOperationException("Missing required configuration: ApiSettings:ApiKey. Please set it in environment variables or user-secrets.");
    }

    builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "BookKiosk",
                ValidAudience = builder.Configuration["Jwt:Audience"] ?? "BookKioskUser",
                IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                    System.Text.Encoding.UTF8.GetBytes(jwtKey))
            };
        });
    builder.Services.AddAuthorization();

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
    
    // Đăng ký Middleware bắt lỗi Global
    app.UseMiddleware<BookKiosk.API.Middleware.GlobalExceptionHandlerMiddleware>();
    
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    // Bật khả năng phục vụ file tĩnh (ảnh, tài liệu) từ thư mục wwwroot
    app.UseStaticFiles();

    app.UseHttpsRedirection();
    
    app.UseCors("AllowAll");
    
    app.UseSerilogRequestLogging();

    // Xác thực bằng API Key (Dành cho máy Kiosk tự phục vụ)
    app.UseMiddleware<BookKiosk.API.Middleware.ApiKeyMiddleware>();

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();
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
