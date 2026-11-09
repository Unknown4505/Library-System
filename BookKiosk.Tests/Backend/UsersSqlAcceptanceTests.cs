extern alias BackendApi;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using BookKiosk.Application.DTOs.Common;
using BookKiosk.Application.Interfaces.Repositories;
using BookKiosk.Application.Services;
using BookKiosk.Domain.Entities;
using BookKiosk.Domain.Enums;
using BookKiosk.Infrastructure.Data;
using BookKiosk.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using BackendApi::BookKiosk.API.Controllers;

namespace BookKiosk.Tests.Backend;

public sealed class UsersSqlAcceptanceTests(DisposableSqlQa db) : IClassFixture<DisposableSqlQa>
{
    [SqlQaFact]
    public async Task Jwt_crud_reset_duplicate_race_and_last_admin_contract_use_real_sql()
    {
        const string keyText = "QA_ONLY_USERS_SQL_KEY_12345678901234567890123456789";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyText));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Jwt:Key"] = keyText }).Build();
        int firstId, secondId;
        await using (var setup = db.Context())
        {
            var first = Admin("qa_admin1"); var second = Admin("qa_admin2");
            setup.Users.AddRange(first, second); await setup.SaveChangesAsync();
            firstId = first.UserId; secondId = second.UserId;
        }
        var gate = new CreateGate();
        using var host = await new HostBuilder().ConfigureWebHost(builder => builder.UseTestServer().ConfigureServices(services =>
        {
            services.AddSingleton<IConfiguration>(config);
            services.AddControllers().AddApplicationPart(typeof(UsersController).Assembly);
            services.Configure<ApiBehaviorOptions>(o => o.InvalidModelStateResponseFactory = _ =>
                new BadRequestObjectResult(ApiResponseDto<object>.Error("BAD_REQUEST", "Dữ liệu không hợp lệ.")));
            services.AddScoped(_ => db.Context());
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IUnitOfWork>(sp => new SynchronizedWork(new UnitOfWork(sp.GetRequiredService<ApplicationDbContext>()), gate));
            services.AddScoped<IUserService, UserService>(); services.AddScoped<IAuthService, AuthService>();
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => o.TokenValidationParameters = new()
            { ValidateIssuer=false, ValidateAudience=false, ValidateLifetime=true, ValidateIssuerSigningKey=true, IssuerSigningKey=key });
            services.AddAuthorization();
        }).Configure(app => {app.UseRouting();app.UseAuthentication();app.UseAuthorization();app.UseEndpoints(e=>e.MapControllers());})).StartAsync();
        using var client = host.GetTestClient();
        using (var response=await client.GetAsync("/api/users")) Assert.Equal(HttpStatusCode.Unauthorized,response.StatusCode);
        foreach(var role in new[]{"Staff","Manager"})
        {
            client.DefaultRequestHeaders.Authorization=new("Bearer",Token(firstId,role,key));
            using var response=await client.GetAsync("/api/users"); Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);
        }
        client.DefaultRequestHeaders.Authorization=new("Bearer",Token(firstId,"Admin",key));
        using(var response=await client.GetAsync("/api/users?page=1&pageSize=1"))
        {
            Assert.Equal(HttpStatusCode.OK,response.StatusCode);
            var body=await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("password",body.ToLowerInvariant());Assert.DoesNotContain("accessToken",body);
            using var json=JsonDocument.Parse(body);Assert.Equal(2,json.RootElement.GetProperty("data").GetProperty("totalCount").GetInt32());
        }
        using(var response=await client.GetAsync("/api/users/2147483647")) Assert.Equal(HttpStatusCode.NotFound,response.StatusCode);
        using(var response=await client.PostAsync("/api/users",Json(new{username="bad space",password="StrongPassword1",fullName="QA User",role="Staff"})))
            await Error(response,HttpStatusCode.BadRequest,"BAD_REQUEST");
        using(var response=await client.PatchAsync($"/api/users/{firstId}/status",Json(new{})))
            await Error(response,HttpStatusCode.BadRequest,"BAD_REQUEST");
        using(var response=await client.PatchAsync($"/api/users/{firstId}/status",Json(new{isActive=false})))
            await Error(response,HttpStatusCode.BadRequest,"BAD_REQUEST");

        const string username="qa_staff";
        int userId;
        using(var response=await Create(username))
        {
            Assert.Equal(HttpStatusCode.Created,response.StatusCode);
            using var body=JsonDocument.Parse(await response.Content.ReadAsStringAsync());userId=body.RootElement.GetProperty("data").GetProperty("userId").GetInt32();
        }
        using(var response=await Create(username.ToUpperInvariant())) await Error(response,HttpStatusCode.Conflict,"CONFLICT");
        gate.Enabled=true;
        var races=await Task.WhenAll(Create("qa_concurrent"),Create("QA_CONCURRENT"));gate.Enabled=false;
        try {Assert.Single(races,x=>x.StatusCode==HttpStatusCode.Created);Assert.Single(races,x=>x.StatusCode==HttpStatusCode.Conflict);}
        finally {foreach(var response in races) response.Dispose();}
        await using(var check=db.Context()) Assert.Equal(1,await check.Users.CountAsync(x=>x.Username.ToLower()=="qa_concurrent"));

        using(var response=await client.PutAsync($"/api/users/{userId}",Json(new{fullName="Updated User",role="Manager",username="not_allowed",isActive=false})))
            Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        await using(var check=db.Context())
        {
            var row=await check.Users.FindAsync(userId);Assert.Equal(username,row!.Username);Assert.True(row.IsActive);Assert.Equal(UserRole.Manager,row.Role);
        }
        using(var response=await client.PostAsync($"/api/users/{userId}/reset-password",Json(new{newPassword="NewPassword2"}))) Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        using(var response=await Login("StrongPassword1")) Assert.Equal(HttpStatusCode.Unauthorized,response.StatusCode);
        using(var response=await Login("NewPassword2")) Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        using(var response=await client.PatchAsync($"/api/users/{userId}/status",Json(new{isActive=false}))) Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        using(var response=await Login("NewPassword2")) Assert.Equal(HttpStatusCode.Unauthorized,response.StatusCode);
        using(var response=await client.PatchAsync($"/api/users/{userId}/status",Json(new{isActive=true}))) Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        using(var response=await Login("NewPassword2")) Assert.Equal(HttpStatusCode.OK,response.StatusCode);

        var demotions=await Task.WhenAll(client.PutAsync($"/api/users/{firstId}",Json(new{fullName="QA Admin One",role="Staff"})),
            client.PutAsync($"/api/users/{secondId}",Json(new{fullName="QA Admin Two",role="Staff"})));
        try {Assert.Single(demotions,x=>x.StatusCode==HttpStatusCode.OK);Assert.Single(demotions,x=>x.StatusCode==HttpStatusCode.Conflict);}
        finally {foreach(var response in demotions) response.Dispose();}
        await using(var check=db.Context()) Assert.Equal(1,await check.Users.CountAsync(x=>x.Role==UserRole.Admin&&x.IsActive));

        Task<HttpResponseMessage> Create(string name)=>client.PostAsync("/api/users",Json(new{username=name,password="StrongPassword1",fullName="QA User",role="Staff"}));
        Task<HttpResponseMessage> Login(string password)=>client.PostAsync("/api/auth/login",Json(new{username,password}));
    }

    private static User Admin(string name)=>new(){Username=name,FullName="QA Admin",Role=UserRole.Admin,IsActive=true,PasswordHash=BCrypt.Net.BCrypt.HashPassword("AdminPassword1",4)};
    private static StringContent Json(object body)=>new(JsonSerializer.Serialize(body),Encoding.UTF8,"application/json");
    private static string Token(int id,string role,SymmetricSecurityKey key)=>new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
        claims:new[]{new Claim(ClaimTypes.NameIdentifier,id.ToString()),new Claim(ClaimTypes.Role,role)},expires:DateTime.UtcNow.AddMinutes(5),
        signingCredentials:new SigningCredentials(key,SecurityAlgorithms.HmacSha256)));
    private static async Task Error(HttpResponseMessage response,HttpStatusCode status,string code)
    {
        Assert.Equal(status,response.StatusCode);using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());Assert.Equal(code,json.RootElement.GetProperty("code").GetString());
    }
    private sealed class CreateGate
    {
        public bool Enabled; public int Arrivals; public readonly TaskCompletionSource Ready=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task WaitAsync(){if(!Enabled)return;if(Interlocked.Increment(ref Arrivals)==2)Ready.TrySetResult();await Ready.Task.WaitAsync(TimeSpan.FromSeconds(30));}
    }
    private sealed class SynchronizedWork(UnitOfWork inner,CreateGate gate):IUnitOfWork
    {
        public Task BeginTransactionAsync()=>inner.BeginTransactionAsync();
        public Task BeginTransactionAsync(System.Data.IsolationLevel isolation)=>inner.BeginTransactionAsync(isolation);
        public Task CommitAsync()=>inner.CommitAsync();public Task RollbackAsync()=>inner.RollbackAsync();
        public async Task<int> SaveChangesAsync(){await gate.WaitAsync();return await inner.SaveChangesAsync();}
        public void Dispose()=>inner.Dispose();
    }
}
